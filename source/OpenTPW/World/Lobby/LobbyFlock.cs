namespace OpenTPW;

/// <summary>Flying actors use the retail clock and target-seeking update.</summary>
public sealed class LobbyFlock : Entity
{
	/// <summary>A retained render slot, enabled only while the simulation has a matching flying actor.</summary>
	private sealed class Actor
	{
		public LobbyModel Model = null!;
	}

	private readonly List<Actor> actors = new();
	private readonly LobbyFlyingMeshConfig config;
	private readonly Vector3 center;
	private readonly Vector3 extents;
	private readonly LobbyQuality quality;
	private readonly LobbyFlockSimulation simulation;
	private int? qualityOverride;
	/// <summary>Follow the live preset unless a caller explicitly overrides the object percentage.</summary>
	public int QualityPercent { get => qualityOverride ?? quality.ObjectsPercent; set => qualityOverride = value; }
	public int ActorCount => simulation.Actors.Count;

	public LobbyFlock( LobbyFlyingMeshConfig config, Vector3 islandPosition, int qualityPercent, uint seed, LobbyQuality? quality = null )
		: this( config, islandPosition, qualityPercent, new LobbyRandom( seed ), quality ) { }

	internal LobbyFlock( LobbyFlyingMeshConfig config, Vector3 islandPosition, int qualityPercent, LobbyRandom random, LobbyQuality? quality = null )
	{
		this.config = config;
		this.quality = quality ?? LobbyQuality.Load();
		center = islandPosition + new Vector3( 0, 0, 50 );
		extents = new Vector3( config.Extents.X, config.Extents.Z, config.Extents.Y );
		if ( qualityPercent != this.quality.ObjectsPercent ) qualityOverride = qualityPercent;
		simulation = new LobbyFlockSimulation( config, center, extents, random );
		var animation = AnimationFiles.Resolve( config.ModelPath, AnimationSlot.M, 0 );
		for ( int i = 0; i < config.CountAtQuality( 100 ); i++ )
			actors.Add( new Actor { Model = new LobbyModel( center, config.ModelPath,
				$"{config.ResourceDirectory}/textures", animation, quality: this.quality ) { Visible = false } } );
	}

	protected override void OnUpdate()
	{
		simulation.Update( RetailTime.FrameTicks, QualityPercent );
		for ( int i = 0; i < actors.Count; i++ ) actors[i].Model.Visible = i < simulation.Actors.Count;
		for ( int i = 0; i < simulation.Actors.Count; i++ )
		{
			var state = simulation.Actors[i];
			actors[i].Model.Position = state.Position;
			actors[i].Model.Rotation = Facing( state.Direction );
		}
	}

	/// <summary>FUN_005d3200: right = normalize(up cross direction), up = direction cross right.</summary>
	public static System.Numerics.Quaternion Facing( Vector3 direction )
	{
		var forward = direction.Normal;
		if ( forward.LengthSquared == 0 ) return System.Numerics.Quaternion.Identity;
		var right = new Vector3( forward.Y, -forward.X, 0 ).Normal;
		if ( right.LengthSquared == 0 ) right = new Vector3( 1, 0, 0 );
		var up = Vector3.Cross( right, forward ).Normal;
		return System.Numerics.Quaternion.CreateFromRotationMatrix( new System.Numerics.Matrix4x4(
			right.X, right.Y, right.Z, 0, forward.X, forward.Y, forward.Z, 0,
			up.X, up.Y, up.Z, 0, 0, 0, 0, 1 ) );
	}
}

/// <summary>One stream is shared by the lobby, as at TP.exe lobby +0x34.</summary>
internal sealed class LobbyRandom
{
	internal uint State { get; private set; }
	internal LobbyRandom( uint seed ) => State = seed;
	internal uint Next()
	{
		uint value = unchecked( State * 0x19660d + 0x3c6ef35f );
		return State = (value >> 13) | (value << 19);
	}
	private float Unit() => (Next() & 0x3fffffff) * (1f / 1073741824f);
	internal Vector3 Point( Vector3 center, Vector3 extents )
	{
		// Consume retail X,Y,Z, then swap Y/Z into the modern coordinate system.
		float x = Unit(), z = Unit(), y = Unit();
		return new Vector3( x * extents.X - extents.X * 0.5f + center.X,
			y * extents.Y - extents.Y * 0.5f + center.Y,
			z * extents.Z - extents.Z * 0.5f + center.Z );
	}
}

/// <summary>0x5d4900 / 0x5d4c20 / 0x5d50f0 without rendering dependencies.</summary>
internal sealed class LobbyFlockSimulation
{
	internal sealed class Actor
	{
		internal Vector3 Position;
		internal Vector3 Direction;
		internal Vector3 Target;
	}
	internal List<Actor> Actors { get; } = new();
	private readonly LobbyFlyingMeshConfig config;
	private readonly Vector3 center;
	private readonly Vector3 extents;
	private readonly LobbyRandom random;
	internal LobbyFlockSimulation( LobbyFlyingMeshConfig config, Vector3 center, Vector3 extents, LobbyRandom random )
	{
		this.config = config;
		this.center = center;
		this.extents = extents;
		this.random = random;
	}
	internal void Update( float dt, int qualityPercent )
	{
		int count = config.CountAtQuality( qualityPercent );
		if ( Actors.Count > count ) Actors.RemoveRange( count, Actors.Count - count );
		while ( Actors.Count < count )
		{
			var position = random.Point( center, extents );
			var target = random.Point( center, extents );
			Actors.Add( new Actor { Position = position, Target = target, Direction = (target - position).Normal } );
			// The final actor consumes no replenishment draw; retail stops on 1, not 0.
			if ( Actors.Count < count && (random.Next() & 7) == 1 ) break;
		}
		foreach ( var actor in Actors )
		{
			var ideal = (actor.Target - actor.Position).Normal;
			actor.Direction = (actor.Direction + (ideal - actor.Direction) * (dt * 0.1f)).Normal;
			actor.Position += actor.Direction * (config.Speed * dt);
			if ( (actor.Target - actor.Position).LengthSquared < 500 )
				actor.Target = random.Point( center, extents );
		}
	}
}
