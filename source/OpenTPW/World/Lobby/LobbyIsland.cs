using System.Numerics;

namespace OpenTPW;

/// <summary>An island and its gate share the ISLAND placement and model rotation.</summary>
public sealed class LobbyIsland : Entity
{
	private readonly LobbyModel island;
	public LobbyGate Gate { get; }
	private readonly string[] islandAnimations;
	private bool islandAnimationStarted;

	public LobbyIsland( Vector3 position, LobbyIslandConfig config, LobbyQuality? quality = null )
		: this( position, config, quality, new LobbyRandom( unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()) ) ) { }

	internal LobbyIsland( Vector3 position, LobbyIslandConfig config, LobbyQuality? quality, LobbyRandom random )
	{
		quality ??= LobbyQuality.Load();
		var placement = GetPlacement( position, config );
		Position = placement.Position;
		Rotation = placement.Rotation;
		var textures = $"{config.ResourceDirectory}/textures";
		// 0x5dc1aa may request either segment, so both are required.
		islandAnimations = Enumerable.Range( 0, 2 ).Select( segment =>
			AnimationFiles.Resolve( config.ModelPath, AnimationSlot.M, segment ) ).ToArray();
		island = new LobbyModel( Position, config.ModelPath, textures, textureResolver:
			name => LobbySigns.Resolve( config.ModelPath, name, config.Name ), quality: quality ) { Rotation = Rotation };
		Gate = new LobbyGate( Position, Rotation, config, quality );
		foreach ( var mesh in config.FlyingMeshes )
			_ = new LobbyFlock( mesh, placement.Target, quality.ObjectsPercent, random, quality );
	}

	internal static (Vector3 Position, Vector3 Target, Quaternion Rotation) GetPlacement( Vector3 position, LobbyIslandConfig config )
	{
		// 0x5daa20 retains the ISLAND height as the orbit/flock anchor, but sets both model roots to Y=0.
		var modelPosition = new Vector3( position.X, position.Y, 0 );
		var target = new Vector3( position.X, position.Y, config.Height );
		// Retail +Y rotation becomes -Z after swapping retail Y/Z (0x5d33c0 -> 0x46ddf0).
		var rotation = Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, -config.RotationDegrees.DegreesToRadians() );
		return (modelPosition, target, rotation);
	}

	protected override void OnUpdate()
	{
		if ( !islandAnimationStarted || island.AnimationFinished )
		{
			// 0x5dc197 selects M1 or M2 with the CRT stream, independently of the lobby LCG.
			int segment = RetailRandom.Next() & 1;
			island.Play( islandAnimations[segment], false );
			islandAnimationStarted = true;
		}
	}
}
