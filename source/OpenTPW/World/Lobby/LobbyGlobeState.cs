using N = System.Numerics;

namespace OpenTPW;

/// <summary>Globe page A (visit parks with players) or B (select a park with room).</summary>
public enum LobbyGlobeMode { Visit, Select }

/// <summary>The fields read from a city object by 0x5d7460 and the A/B predicates.</summary>
public sealed record LobbyGlobePlace( string Name, N.Vector3 Position, int Count, int State, int Capacity )
{
	public string ModelName => Name.Equals( "North Pole", StringComparison.OrdinalIgnoreCase ) ? "bullfrog" : "hpin";
	public float Size => Math.Clamp( Count * 0.0005f + 0.02f, 0.02f, 0.1f );
	public N.Vector3 Scale => new( 0.035f + 0.2f * Size, Size, 0.035f + 0.2f * Size );
	public bool VisibleIn( LobbyGlobeMode mode ) => mode == LobbyGlobeMode.Visit ? Count > 0 : State == 0 && Count < Capacity;
}

/// <summary>Globe camera and hit testing, 0x5d7600/0x5d78d0/0x5d8450.</summary>
public sealed class LobbyGlobeState
{
	private readonly float far, near;
	private float yawVelocity, pitchVelocity;
	public bool Zoomed { get; private set; }
	public float Yaw { get; private set; }
	public float Pitch { get; private set; }
	public float Distance { get; private set; }
	public float Sensitivity => Zoomed ? 0.2f : 0.4f;
	public float KeyStep => Zoomed ? 0.075f : 0.15f;
	public N.Vector3 Position => new( MathF.Cos( Yaw ) * MathF.Cos( Pitch ) * Distance,
		MathF.Sin( Yaw ) * MathF.Cos( Pitch ) * Distance, MathF.Sin( Pitch ) * Distance );
	public LobbyGlobeState( LobbyConfig config ) { far = config.GlobeRadiusOut; near = config.GlobeRadiusIn; Reset(); }
	public void Reset() { Yaw = Pitch = yawVelocity = pitchVelocity = 0; Zoomed = false; Distance = far; }
	public void ToggleZoom() => Zoomed = !Zoomed;
	public void Turn( int horizontal, int vertical ) { yawVelocity += horizontal * KeyStep; pitchVelocity += vertical * KeyStep; }
	public void Update( float ticks, N.Vector2 mouse )
	{
		mouse = N.Vector2.Clamp( mouse, N.Vector2.Zero, N.Vector2.One );
		float Edge( float value ) => value < 0.01f ? value - 0.01f : value > 0.99f ? value - 0.99f : 0;
		yawVelocity += Edge( mouse.X ) * Sensitivity * 50;
		pitchVelocity -= Edge( mouse.Y ) * Sensitivity * 50;
		float damping = Zoomed ? 0.25f : 0.5f;
		yawVelocity *= damping; pitchVelocity *= damping;
		Yaw += ticks * yawVelocity;
		Pitch += ticks * pitchVelocity;
		if ( Pitch > 1.5f || Pitch < -1.5f ) { Pitch = Math.Clamp( Pitch, -1.5f, 1.5f ); pitchVelocity = 0; }
		// 0x6feac0 is -0.3f; subtraction produces convergence toward the selected radius.
		Distance += ((Zoomed ? near : far) - Distance) * ticks * 0.3f;
	}
	public static LobbyGlobePlace? Pick( N.Vector3 origin, N.Vector3 direction, IEnumerable<LobbyGlobePlace> places, LobbyGlobeMode mode )
	{
		if ( direction.LengthSquared() == 0 ) return null;
		direction = N.Vector3.Normalize( direction );
		float along = N.Vector3.Dot( -origin, direction );
		float perpendicular = (-origin - along * direction).LengthSquared();
		if ( perpendicular >= 116 * 116 ) return null;
		float delta = MathF.Sqrt( 116 * 116 - perpendicular );
		float distance = along - delta;
		if ( distance < 0 ) distance = along + delta;
		if ( distance <= 0 ) return null;
		var point = origin + distance * direction;
		LobbyGlobePlace? selected = null;
		float nearest = float.MaxValue;
		foreach ( var place in places )
		{
			if ( !place.VisibleIn( mode ) ) continue;
			float squared = N.Vector3.DistanceSquared( point, place.Position );
			if ( squared < 100 && squared < nearest ) { nearest = squared; selected = place; }
		}
		return selected;
	}
}
