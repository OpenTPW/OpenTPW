using System.Numerics;

namespace OpenTPW;

/// <summary>A gate holds its base pose until the lobby camera enters or leaves its entrance.</summary>
public sealed class LobbyGate : Entity
{
	private readonly LobbyModel model;
	private readonly string enteringAnimation;
	private readonly string leavingAnimation;
	private readonly LobbyGatePlayback playback;
	internal bool HasPendingAnimation => playback.HasPendingSegment;
	internal bool AnimationFinished => model.AnimationFinished;

	internal LobbyGate( Vector3 position, Quaternion rotation, LobbyIslandConfig config, LobbyQuality quality )
	{
		enteringAnimation = AnimationFiles.Resolve( config.GateModelPath, AnimationSlot.M, 0 );
		leavingAnimation = AnimationFiles.Resolve( config.GateModelPath, AnimationSlot.M, 1 );
		model = new LobbyModel( position, config.GateModelPath, $"{config.ResourceDirectory}/textures", textureResolver:
			name => LobbySigns.Resolve( config.GateModelPath, name, config.Name ), quality: quality ) { Rotation = rotation };
		playback = new LobbyGatePlayback( segment => model.Play( segment == 0 ? enteringAnimation : leavingAnimation, false ) );
		// Lobby_CreateModel (US 0x5d8870) creates an instance without starting slot M.
	}

	/// <summary>Plays M1 once when the camera has aligned with the entrance (Lobby_UpdateCameraAndWeather (US 0x5e0470), position pending).</summary>
	public void StartEntering() => playback.Request( 0, model.AnimationFinished );

	/// <summary>Plays M2 once when an entrance transition is cancelled (LobbyCamera_CancelEntry (US 0x5e1350)).</summary>
	public void StartLeaving() => playback.Request( 1, model.AnimationFinished );

	protected override void OnUpdate() => playback.Update( model.AnimationFinished );
}

/// <summary>ModelAnimation_QueueSegment (US 0x4732a0) defers the latest request until the current non-looping segment finishes.</summary>
internal sealed class LobbyGatePlayback
{
	private readonly Action<int> play;
	private bool started;
	private int? pending;
	internal bool HasPendingSegment => pending.HasValue;

	internal LobbyGatePlayback( Action<int> play ) => this.play = play;

	internal void Request( int segment, bool finished )
	{
		if ( started && !finished )
			pending = segment;
		else
			Start( segment );
	}

	internal void Update( bool finished )
	{
		if ( finished && pending is int segment ) Start( segment );
	}

	private void Start( int segment )
	{
		pending = null;
		started = true;
		play( segment );
	}
}
