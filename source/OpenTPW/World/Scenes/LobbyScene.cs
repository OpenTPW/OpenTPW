using OpenTPW.UI;
using System.Diagnostics.CodeAnalysis;

namespace OpenTPW;

public class LobbyScene
{
	// A small delay keeps each loading stage visible in the progress overlay.
	private const float LoadingStageInterval = .1f;
	// Reconstructed starting focus, in world units along the current view.
	private const float EntryFocusDistance = 70;
	public ParkScene? ActivePark { get; private set; }
	private List<Actor>? lobbyActors;
	private ParkScene? loadingPark;
	private float entryStarted;
	private float nextLoadingStage;
	public bool IsEnteringPark { get; private set; }
	public bool IsLoadingPark => loadingPark != null;

	public void EnterPark()
	{
		if ( SelectedPlayer == null || ActivePark != null || IsEnteringPark )
		{
			return;
		}
		var island = Actor.All.OfType<LobbyIsland>().First( a => a.IslandIndex == SelectedIsland );
		IsOverview = false;
		IsEnteringPark = true;
		entryStarted = Time.Now;
		ParkEntryCameraMode.StartPosition = Camera.Position;
		ParkEntryCameraMode.StartFocus = Camera.Position + Camera.Rotation.Forward * EntryFocusDistance;
		ParkEntryCameraMode.StartFov = Camera.FieldOfView;
		ParkEntryCameraMode.Gate = island.GatePosition;
		ParkEntryCameraMode.Outward = island.GateOutward;
		island.OpenGate();
		Camera.SetCameraMode<ParkEntryCameraMode>();
	}

	public void ReturnToLobby()
	{
		if ( ActivePark == null )
		{
			return;
		}

		Actor.DeleteAll();
		Actor.All = lobbyActors!;
		lobbyActors = null;
		ActivePark = null;
		RestoreLobby();
	}

	private void RestoreLobby()
	{
		IsEnteringPark = false;
		loadingPark = null;
		RootPanel.Instance = Hud;
		foreach ( var island in Actor.All.OfType<LobbyIsland>() )
		{
			island.ResetGate();
		}
		Camera.SetCameraMode<LobbyCameraMode>();
		LoadingScreen.Visible = false;
		LoadingScreen.ParkName = null;
	}

	private void LoadPark()
	{
		try
		{
			if ( loadingPark == null )
			{
				BeginParkLoading();
				return;
			}

			if ( Time.Now < nextLoadingStage )
			{
				return;
			}

			nextLoadingStage = Time.Now + LoadingStageInterval;
			if ( !loadingPark.AdvanceLoading() )
			{
				return;
			}

			ActivePark = loadingPark;
			loadingPark = null;
			IsEnteringPark = false;
			LoadingScreen.Visible = false;
			LoadingScreen.ParkName = null;
		}
		catch ( Exception ex )
		{
			Actor.DeleteAll();

			if ( lobbyActors != null )
			{
				Actor.All = lobbyActors;
			}

			lobbyActors = null;
			RestoreLobby();
			Log.Error( ex.ToString() );
			Hud.AddChild( new Dialog( "Unable to enter this park.\n\n" + ex.Message ) );
		}
	}

	private void BeginParkLoading()
	{
		// Retain the lobby actors so returning restores their state. The park
		// registers its own actors and root panel while its stages run.
		lobbyActors = Actor.All;
		Actor.All = new();
		RootPanel.Instance = null;
		LoadingScreen.Progress = 0;
		LoadingScreen.ParkName = IslandName;
		LoadingScreen.Visible = true;
		loadingPark = new ParkScene( SelectedIsland, SelectedPlayer!, ReturnToLobby, IslandName );
		nextLoadingStage = Time.Now + LoadingStageInterval;
	}

	internal static LobbyScene Current { get; set; } = null!;
	public PlayerProfile? SelectedPlayer { get; internal set; }
	public int SelectedIsland { get; private set; }
	public bool IsOverview { get; private set; }

	public Vector3 IslandTarget => ParkThemes.Get( SelectedIsland ).LobbyFocus;

	public string IslandName => ParkThemes.Get( SelectedIsland ).DisplayName;

	public void SelectPlayer( PlayerProfile profile )
	{
		SelectedPlayer = profile;
		SelectedIsland = 0;
		IsOverview = false;
		Hud.ClearChildren();

		Hud.AddChild( new IslandSelectionPanel( this ) );
		Camera.SetCameraMode<LobbyCameraMode>();
	}

	public void SwitchIsland( int direction )
	{
		int nextIsland = (SelectedIsland + direction) % ParkThemes.All.Count;
		SelectedIsland = nextIsland < 0 ? nextIsland + ParkThemes.All.Count : nextIsland;
		IsOverview = false;
	}

	public void ToggleOverview() => IsOverview = !IsOverview;

	public void ShowPlayers()
	{
		SelectedPlayer = null;
		IsOverview = false;
		Hud.ClearChildren();

		new LobbyLayout
		{
			Hud = Hud
		}.OnInit();
		Camera.SetCameraMode<PlayerSelectCameraMode>();
	}

	public RootPanel Hud { get; set; }
	public SunActor SunLight { get; set; }

	public LobbyScene()
	{
		Current = this;
		SetupHud();
		SetupEntities();
	}

	[MemberNotNull( nameof( SunLight ) )]
	private void SetupEntities()
	{
		SunLight = new SunActor()
		{
			Position = new( 0, 100, 100 )
		};
		_ = new WaterActor();
		_ = new SkyActor();
		foreach ( var file in FileSystem.GetFiles( "/lobby/" ) )
		{
			Log.Info( file );
		}

		try
		{
			foreach ( var theme in ParkThemes.All )
			{
				_ = new LobbyIsland( theme.LobbyPosition, theme.LobbyName );
			}
		}
		catch ( Exception ex )
		{
			Log.Error( ex.Message );
		}

		Camera.SetCameraMode<PlayerSelectCameraMode>();
	}

	[MemberNotNull( nameof( Hud ) )]
	private void SetupHud()
	{
		Hud = new();
		var layout = new LobbyLayout()
		{
			Hud = Hud
		};
		layout.OnInit();
	}

	public void Update()
	{
		if ( IsEnteringPark )
		{
			if ( Time.Now - entryStarted >= ParkEntryCameraMode.Duration )
			{
				LoadPark();
			}

			return;
		}

		if ( ActivePark != null )
		{
			ActivePark.Update();
			return;
		}

		Actor.UpdateAll();
		Cursor.Update();
	}

	private readonly Cursor Cursor = new();

	public void RenderUI()
	{
		if ( ActivePark != null )
		{
			ActivePark.RenderUI();
			return;
		}

		if ( IsEnteringPark )
		{
			return;
		}

		Actor.RenderUIAll();
		Cursor.Draw();
	}

	public void Render()
	{
		if ( ActivePark != null )
		{
			ActivePark.Render();
			return;
		}

		if ( IsLoadingPark )
		{
			return;
		}

		Actor.RenderAll();
	}
}
