using OpenTPW.UI;

namespace OpenTPW;

/// <summary>The running scene. Currently always the lobby: Base, sky, weather, islands, flocks, the HUD pages and their wiring.</summary>
public class Level
{
	internal static Level Current { get; set; }

	public RootPanel Hud { get; set; }
	public Sun SunLight { get; set; }
	public LobbyConfig? Lobby { get; private set; }
	public Sky? EnvironmentSky { get; private set; }
	public AudioSystem? Audio { get; }
	public LobbyAudio? LobbySound { get; }
	public LobbyAdvisor? Advisor { get; private set; }
	internal LobbyPlayerPanel? PlayerMenu { get; set; }
	internal LobbyNavigation? Navigation { get; private set; }
	internal LobbyIslandPanel? IslandMenu { get; private set; }
	internal LobbyGlobePanel? GlobeMenu { get; private set; }
	internal LobbyMenuPanel? Menu { get; private set; }
	internal LobbyOptionsPanel? Options { get; private set; }
	internal LobbyPlayerProfile? SelectedPlayer => selectedPlayer;
	public bool FogEnabled => GlobeMenu?.Active != true;
	/// <summary>Scene lighting: the globe page has its own ambient and light (0x5d8060), every other page the islands' (0x5dc390).</summary>
	internal LobbyLighting Lighting => GlobeMenu?.Active == true ? LobbyLighting.Globe : LobbyLighting.Islands;
	/// <summary>Raised when the entry flight completes. No park loader consumes it yet; without a handler the island page is restored.</summary>
	public event Action<LobbyPlayerProfile, LobbyIslandConfig>? ParkEntryRequested;
	private LobbyPlayerProfile? selectedPlayer;
	private LobbyKeyParticlePanel? keyEffects;
	internal LobbyKeyParticlePanel? KeyEffects => keyEffects;
	/// <summary>Hover help strip shared by the lobby pages and dialogs (0x7ca0c0).</summary>
	internal LobbyHelpBar? Help { get; private set; }

	public SettingsFile Global { get; private init; }

	public Level( string levelName, AudioSystem? audio = null )
	{
		Audio = audio;
		LobbySound = audio == null ? null : new LobbyAudio( audio );
		Global = new SettingsFile( $"/levels/{levelName}/global.sam" );
		Current = this;

		SetupEntities();
		SetupHud();
	}

	private void SetupEntities()
	{
		SunLight = new Sun() { Position = new( 0, 100, 100 ) };

		// 0x5d34e0 -> 0x44f710 loads the authored lobby Base, including both sea meshes and their UVs.
		_ = new LobbyModel( Vector3.Zero, "lobby/terrain/Base.md2", "lobby/terrain/textures" );
		var sky = new Sky();
		EnvironmentSky = sky;
		Lobby = LobbyConfig.Load();
		// 0x5d07a0 seeds the lobby stream (lobby +0x34) from the clock in seconds; the camera,
		// weather and island flocks all draw from it, so their construction order matters.
		var lobbyRandom = new LobbyRandom( unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()) );
		LobbyCameraMode.Configure( Lobby, lobbyRandom );
		_ = new LobbyWeather( sky, () => LobbyCameraMode.CurrentIsland, Lobby, lobbyRandom, Audio );
		var islands = new Dictionary<int, LobbyIsland>();
		foreach ( var island in Lobby.Islands )
		{
			var center = Lobby.CameraPositions[island.Id];
			islands.Add( island.Id, new LobbyIsland( new Vector3( center.X, center.Y, 0 ), island, null, lobbyRandom ) );
		}
		LobbyCameraMode.GateAnimationRequested += ( id, entering ) =>
		{
			if ( entering ) islands[id].Gate.StartEntering();
			else islands[id].Gate.StartLeaving();
		};

		Camera.SetCameraMode<LobbyCameraMode>();
	}

	private void SetupHud()
	{
		Hud = new();

		var layout = new LobbyLayout() { Hud = Hud };
		layout.OnInit();
		Navigation = new LobbyNavigation( Lobby! );
		Navigation.IslandSelected += LobbyCameraMode.SelectIsland;
		Navigation.EntryRequested += () =>
		{
			// 0x5dcd00 -> 0x4b6a30 plays global lobby effect 4 when leaving island selection.
			Audio?.Play( "global/sound", "cat_globallobbysfx", 4 );
			keyEffects?.EnterIsland();
			LobbyCameraMode.BeginEntering();
		};
		Navigation.EntryCancelled += LobbyCameraMode.CancelEntering;
		IslandMenu = Hud.AddChild( new LobbyIslandPanel( Navigation ) );
		GlobeMenu = Hud.AddChild( new LobbyGlobePanel( this ) );
		IslandMenu.GlobeRequested += () => GlobeMenu.Show( LobbyGlobeMode.Visit );
		PlayerMenu!.PlayerSelected += ( player, data ) =>
		{
			selectedPlayer = player;
			Navigation.Bind( data );
			IslandMenu.Activate();
		};
		var confirmation = new LobbyConfirmPanel( Hud );
		Menu = new LobbyMenuPanel( this, confirmation );
		PlayerMenu.QuitRequested += () => confirmation.Show( Localization.Parse( "#ConfirmQuit" ), () => Window.Current.SdlWindow.Close() );
		LobbyCameraMode.IslandEntered += id =>
		{
			if ( ParkEntryRequested != null ) ParkEntryRequested( selectedPlayer!, Lobby!.Islands.First( island => island.Id == id ) );
			else
			{
				Navigation.CancelEntry();
				IslandMenu.Failure = "Park loading is not available in this build.";
				Log.Warning( $"Park entry has no handler: {selectedPlayer!.Directory}, {Navigation.Island.ThemeName}" );
			}
		};
		if ( Audio != null )
		{
			Advisor = new LobbyAdvisor( Audio );
			if ( LobbyOptionsSettings.Current.Preferences is { } defaults ) Advisor.Enabled = defaults[32] != 0;
			Advisor.ResponseCue += response =>
			{
				if ( response == 0x189 )
				{
					// 0x595d30 pairs emitter 87 with the separately registered UI sound category.
					LobbySound!.IntroductionKey();
					keyEffects?.IntroductionKey();
				}
			};
			Advisor.Welcome( LobbyPlayerProfiles.Read().Any( player => player != null ) );
			Hud.AddChild( new LobbyAdvisorPanel( Advisor ) );
		}

		keyEffects = Hud.AddChild( new LobbyKeyParticlePanel() );
		Hud.AddChild( new LobbyFadePanel() );
		Hud.AddChild( Menu ); Hud.AddChild( confirmation );
		// Options cover the Advisor and existing pages, while the cursor remains the final UI layer.
		Options = Hud.AddChild( new LobbyOptionsPanel( this ) );
		Menu.OptionsRequested += Options.Show;
		Help = Hud.AddChild( new LobbyHelpBar() );
		Hud.AddChild( new Cursor() );
	}

	internal void ReturnToPlayers()
	{
		// 0x489d10 unloads the selected profile (0x5c3740) before reopening 0x4a4010.
		GlobeMenu?.Hide(); Navigation!.Unbind(); IslandMenu!.Active = false;
		selectedPlayer = null; PlayerMenu!.Back(); Advisor?.Cancel();
		LobbyCameraMode.BeginRoaming();
	}
	internal void HandleEscape()
	{
		// 0x5dee20 first asks the active page to consume Escape. Naming/globe/modals
		// keep their own cancellation; an entering island consumes it before the shared menu.
		if ( Hud.Modal != null || GlobeMenu?.Active == true || !(PlayerMenu!.Browsing || Navigation!.Active) ) return;
		if ( Navigation!.Entering ) Navigation.CancelEntry();
		else Menu!.Show();
	}
	public void Update()
	{
		Audio?.Update();
		if ( Input.Pressed( Veldrid.Key.Escape ) ) HandleEscape();
		// 0x54a970 updates 0x595d30 and the particle clock outside UI dispatch.
		// Modal input routing must not freeze speech/lips or the authored particle chain.
		Advisor?.Update( Time.Now );
		keyEffects?.SetAffordable( IslandMenu?.Active == true && Navigation?.CanEnter == true );
		keyEffects?.Advance( Time.Delta );
		// 0x5db430 updates the camera before calculating distance-controlled weather.
		Camera.Update();
		if ( GlobeMenu?.Active != true ) LobbySound?.SetIsland( LobbyCameraMode.CurrentIsland.ThemeName );
		// Flocks can add model entities while replenishing their actor list.
		foreach ( var entity in Entity.All.ToArray() )
			if ( GlobeMenu?.Active != true || entity == Hud || GlobeMenu.Owns( entity ) ) entity.Update();
	}

	public void Render()
	{
		// Retail registers screen-space UI models separately (TP.exe 0x474eb0 ->
		// 0x45a960, list mask 0x20; screen transform at 0x474f10).
		// Keep the OpenTPW HUD out of world creation order as well.
		foreach ( var entity in Entity.InRenderOrder( Entity.All ) )
		{
			if ( GlobeMenu != null && entity != Hud && (GlobeMenu.Active != GlobeMenu.Owns( entity )) ) continue;
			// Material.UI disables depth testing and writes, so world depth cannot hide the HUD.
			entity.Render();
		}
	}
}
