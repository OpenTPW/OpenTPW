using System.Text.Json;

namespace OpenTPW;

/// <summary>
/// Lobby camera: drives <see cref="LobbyCameraState"/> with retail frame ticks and exposes
/// island selection, gate and entry events to the lobby. A debug fixed view replaces the
/// controller for same-viewpoint comparison with the retail game.
/// </summary>
public sealed class LobbyCameraMode : CameraMode
{
	public static LobbyConfig Config { get; private set; } = null!;
	public static int SelectedIsland { get; private set; }
	private static LobbyCameraState state = null!;
	internal static event Action<int, bool>? GateAnimationRequested;
	internal static event Action<int>? IslandEntered;
	public static float EntryFade => state?.EntryFade ?? 0;
	internal static LobbyCameraState.Movement Movement => state.Mode;
	private Vector3 trackedTarget;
	private static FixedView? fixedView;
	private bool reportedFixedView;
	/// <summary>
	/// Debug-only fixed view file (<c>--lobby-view</c>). Read on <see cref="Configure(LobbyConfig, LobbyRandom, int)"/>;
	/// null runs the normal controller.
	/// </summary>
	internal static string? FixedViewPath { get; set; }
	/// <summary>Fixed comparison view in retail Y-up coordinates; vertical FOV in degrees.</summary>
	private sealed record FixedView( int FormatVersion, string CoordinateSystem, int IslandId,
		float[] Position, float[] Target, float VerticalFieldOfViewDegrees, FixedSky? Sky );
	/// <summary>
	/// Optional retail cloud state read from the sky object (US 0x8c1828): layer count +0x2ec8, per-layer U/V
	/// offsets +0x1fa0/+0x1fa8 + layer × 0xc, and the low byte of each per-vertex colour table +0xf60 + layer × 0x400.
	/// </summary>
	internal sealed record FixedSky( int Layers, float[][] Offsets, int[][] Greys );
	/// <summary>Retail cloud state to replay while a fixed view is active, or null.</summary>
	internal static FixedSky? FixedViewSky => fixedView?.Sky;
	public static LobbyIslandConfig CurrentIsland => Config.Islands.First( x => x.Id == SelectedIsland );

	public static void Configure( LobbyConfig config, int island = 0 )
		=> Configure( config, new LobbyRandom( unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()) ), island );

	internal static void Configure( LobbyConfig config, LobbyRandom random, int island = 0 )
	{
		Config = config;
		GateAnimationRequested = null;
		IslandEntered = null;
		fixedView = ReadFixedView();
		state = new LobbyCameraState( config, random );
		SelectedIsland = fixedView?.IslandId ?? island;
		// The login page has no player/theme binding yet. Retail uses its roaming
		// branch for a player whose theme is -1 (0x5db430), rather than orbiting island 0.
		if ( fixedView != null ) state.SelectIsland( fixedView.IslandId );
	}

	private static FixedView? ReadFixedView()
	{
		var path = FixedViewPath;
		if ( string.IsNullOrWhiteSpace( path ) ) return null;
		var view = JsonSerializer.Deserialize<FixedView>( File.ReadAllText( path ),
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );
		if ( view == null || view.FormatVersion != 1 || view.CoordinateSystem != "retail-y-up" )
			throw new InvalidDataException( "Fixed lobby view requires formatVersion 1 and coordinateSystem retail-y-up." );
		if ( !ValidVector( view.Position ) || !ValidVector( view.Target ) ||
			!float.IsFinite( view.VerticalFieldOfViewDegrees ) || view.VerticalFieldOfViewDegrees <= 0 || view.VerticalFieldOfViewDegrees >= 180 )
			throw new InvalidDataException( "Fixed lobby view requires finite position/target vectors and a vertical field of view between 0 and 180 degrees." );
		var direction = FromRetail( view.Target ) - FromRetail( view.Position );
		if ( !float.IsFinite( direction.LengthSquared ) || direction.LengthSquared <= 0 ||
			!Config.Islands.Any( item => item.Id == view.IslandId ) )
			throw new InvalidDataException( "Fixed lobby view requires a known island and distinct position/target." );
		return view;
	}

	private static bool ValidVector( float[]? vector ) => vector is { Length: 3 } && vector.All( float.IsFinite );
	// LobbyModel maps retail (x,y,z) to OpenTPW (x,z,y); 0x5d3eb0 uses retail Y as up.
	private static Vector3 FromRetail( float[] vector ) => new( vector[0], vector[2], vector[1] );

	public static void SelectIsland( int id )
	{
		state.SelectIsland( id );
		SelectedIsland = id;
	}

	public static void BeginEntering() => state.BeginEntering();
	public static void BeginRoaming() => state.BeginRoaming();
	public static void CancelEntering()
	{
		if ( state.CancelEntering() ) GateAnimationRequested?.Invoke( SelectedIsland, false );
	}

	public override void Update()
	{
		if ( Config == null ) return;
		if ( fixedView != null )
		{
			Position = FromRetail( fixedView.Position );
			trackedTarget = FromRetail( fixedView.Target );
			Rotation = Rotation.LookAt( trackedTarget - Position );
			FieldOfView = fixedView.VerticalFieldOfViewDegrees;
			Level.Current?.Lighting.Track( Position.GetSystemVector3(), trackedTarget.GetSystemVector3() );
			if ( !reportedFixedView )
			{
				Log.Info( $"Fixed lobby view active: island {fixedView.IslandId}" );
				reportedFixedView = true;
			}
			return;
		}
		state.Update( RetailTime.FrameTicks );
		Position = state.Position;
		trackedTarget = state.Target;
		SelectedIsland = state.CurrentIslandId;
		if ( state.GateOpened ) GateAnimationRequested?.Invoke( SelectedIsland, true );
		if ( state.EntryCompleted ) IslandEntered?.Invoke( SelectedIsland );
		Rotation = Rotation.LookAt( trackedTarget - Position );
		// 0x5d3eb0 re-aims the camera's light at the target on every camera update.
		Level.Current?.Lighting.Track( Position.GetSystemVector3(), trackedTarget.GetSystemVector3() );
		// Retail's viewport/frustum spans +/-0.5, rather than the modern +/-1 convention: 0x5751e0 builds the
		// projection and 0x5747c0 post-scales Y by -2 (100 degrees becomes 61.579468 vertical).
		FieldOfView = (2 * MathF.Atan( 0.5f * MathF.Tan( Config.IslandFovDegrees.DegreesToRadians() * 0.5f ) )).RadiansToDegrees();
	}
}


/// <summary>TP.exe 0x5dac90, 0x5db430 and 0x5dc1d0, in modern Z-up coordinates.</summary>
internal sealed class LobbyCameraState
{
	/// <summary>Controller phase: free roam, orbit an island, align with its doorway, fly in, done.</summary>
	internal enum Movement { Roaming, Orbiting, Aligning, Entering, Entered }
	private readonly LobbyConfig config;
	private readonly LobbyRandom random;
	private Vector3 roamPosition, roamDestination, roamDirection;
	private Vector3 roamTarget, desiredTarget, targetDirection;
	private float targetSpeed = 2;
	internal Vector3 Position { get; private set; }
	internal Vector3 Target { get; private set; }
	/// <summary>Orbit angle in radians, kept in [0, 2π) as 0x5db430 does.</summary>
	internal float Angle { get; private set; }
	internal float Radius { get; private set; }
	internal float Height { get; private set; }
	/// <summary>Fade to black during entry: 0 until the flight starts, 1 when the radius reaches 8.</summary>
	internal float EntryFade { get; private set; }
	internal int CurrentIslandId { get; private set; }
	internal Movement Mode { get; private set; } = Movement.Roaming;
	internal bool GateOpened { get; private set; }
	internal bool EntryCompleted { get; private set; }
	private static readonly Vector3 RoamCenter = new( 500, 500, 75 );
	private static readonly Vector3 RoamExtents = new( 400, 400, 50 );

	internal LobbyCameraState( LobbyConfig config, LobbyRandom random )
	{
		this.config = config;
		this.random = random;
		CurrentIslandId = config.Islands[0].Id;
		// Constructor consumes all four X/Y/Z points even when a player later
		// selects an island. Their order is shared with the weather/flock stream.
		roamPosition = random.Point( RoamCenter, RoamExtents );
		roamDestination = random.Point( RoamCenter, RoamExtents );
		roamDirection = Direction( roamDestination - roamPosition );
		roamTarget = random.Point( RoamCenter, RoamExtents );
		desiredTarget = random.Point( RoamCenter, RoamExtents );
		targetDirection = Direction( desiredTarget - roamTarget );
	}

	internal void SelectIsland( int id )
	{
		_ = config.Islands.First( island => island.Id == id );
		CurrentIslandId = id;
		Mode = Movement.Orbiting;
		EntryFade = 0;
	}

	internal void BeginRoaming()
	{
		Mode = Movement.Roaming;
		EntryFade = 0;
	}

	internal void BeginEntering()
	{
		// Entry first aligns the orbit with the authored doorway; the gate opens on that transition.
		if ( Mode == Movement.Orbiting ) Mode = Movement.Aligning;
	}

	internal bool CancelEntering()
	{
		// Cancelling alignment leaves the closed gate alone. Only an opened gate queues M2.
		bool closeGate = Mode == Movement.Entering || Mode == Movement.Entered;
		if ( Mode != Movement.Roaming ) Mode = Movement.Orbiting;
		EntryFade = 0;
		return closeGate;
	}

	/// <summary>Advances the controller by <paramref name="dt"/> retail ticks (1 = 100 ms, clamped to 5).</summary>
	internal void Update( float dt )
	{
		dt = Math.Clamp( dt, 0, 5 );
		GateOpened = EntryCompleted = false;
		if ( Mode == Movement.Roaming )
		{
			UpdateRoaming( dt );
			return;
		}
		var island = config.Islands.First( item => item.Id == CurrentIslandId );
		var center = config.CameraPositions[island.Id];
		var anchor = new Vector3( center.X, center.Y, island.Height );
		float entranceAngle = island.RotationDegrees.DegreesToRadians() + MathF.PI;
		if ( Mode == Movement.Orbiting )
		{
			Angle = Wrap( Angle + config.SpinSpeed * dt );
			Radius = config.SpinRadius;
			Height = config.VerticalOffset;
		}
		else if ( Mode == Movement.Aligning )
		{
			Radius = config.SpinRadius;
			Height = config.VerticalOffset;
			float difference = Wrap( Angle - Wrap( entranceAngle ) );
			float step = dt * 0.05f;
			if ( difference < step * 0.5f || difference > MathF.Tau - step * 0.5f )
			{
				Mode = Movement.Entering;
				GateOpened = true;
			}
			else Angle = Wrap( entranceAngle + difference + (difference < MathF.PI ? -step : step) );
		}
		else if ( Mode == Movement.Entering )
		{
			// 0x5db430 exponentially reduces radius/height in retail dt units (100 ms).
			Angle = entranceAngle;
			Radius -= dt * Radius * 0.07f;
			Height -= dt * Height * 0.6f;
			float range = config.SpinRadius - 8;
			if ( range == 0 ) range = 1;
			EntryFade = Radius >= 8 ? 1 - (Radius - 8) / range : 1;
			if ( Radius < 8 )
			{
				Mode = Movement.Entered;
				EntryCompleted = true;
			}
		}
		var desiredPosition = anchor + new Vector3( MathF.Sin( Angle ) * Radius, MathF.Cos( Angle ) * Radius, Height );
		Position += (desiredPosition - Position) * (dt * 0.1f);
		Target += (anchor - Target) * (dt * 0.2f);
		roamPosition = Position;
		roamTarget = Target;
	}

	private void UpdateRoaming( float dt )
	{
		roamDirection = Direction( roamDirection + (Direction( roamDestination - roamPosition ) - roamDirection) * (dt * 0.1f) );
		roamPosition += roamDirection * dt;
		if ( (roamDestination - roamPosition).LengthSquared < 100 ) roamDestination = random.Point( RoamCenter, RoamExtents );
		// Native compares each island against the previous frame's +0x28 pose,
		// then copies this frame's roaming pose there after target integration.
		float nearestDistance = 9999999;
		foreach ( var island in config.Islands )
		{
			var center = config.CameraPositions[island.Id];
			var anchor = new Vector3( center.X, center.Y, island.Height );
			float distance = (anchor - Position).LengthSquared;
			if ( distance < nearestDistance )
			{
				nearestDistance = distance;
				CurrentIslandId = island.Id;
				desiredTarget = anchor;
			}
		}
		targetDirection = Direction( targetDirection + (Direction( desiredTarget - roamTarget ) - targetDirection) * (dt * 0.1f) );
		roamTarget += targetDirection * (dt * targetSpeed);
		if ( (desiredTarget - roamTarget).LengthSquared >= 50 )
		{
			if ( targetSpeed >= 2 ) targetSpeed = 2;
			else targetSpeed += 0.1f;
		}
		else if ( targetSpeed <= 0 ) targetSpeed = 0;
		else targetSpeed -= 0.1f;
		Position = roamPosition;
		Target = roamTarget;
	}

	private static Vector3 Direction( Vector3 delta ) => delta.LengthSquared == 0 ? new Vector3( 1, 0, 0 ) : delta.Normal;
	private static float Wrap( float angle )
	{
		// Repeated subtraction matches retail rounding; a non-finite angle would never terminate.
		if ( !float.IsFinite( angle ) ) throw new InvalidDataException( $"Lobby camera angle is not finite: {angle}" );
		while ( angle >= MathF.Tau ) angle -= MathF.Tau;
		while ( angle < 0 ) angle += MathF.Tau;
		return angle;
	}
}
