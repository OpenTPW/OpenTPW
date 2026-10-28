#if DEBUG
using System.Text.Json;

namespace OpenTPW;

/// <summary>Opt-in scene acceptance using real page, camera, gate and renderer instances.</summary>
internal sealed class LobbyAcceptance
{
	private readonly Level level;
	private readonly string output;
	private readonly List<string> checks = new();
	private readonly double beginning = Time.Now;
	private double changed = Time.Now;
	private int stage, visited, opened, closed;
	private LobbyGate? cancellingGate;
	private LobbyConfirmPanel Confirmation => level.Hud.Children.OfType<LobbyConfirmPanel>().Single();
	internal LobbyAcceptance( Level level, string output )
	{
		this.level = level;
		this.output = Path.GetFullPath( output );
		Directory.CreateDirectory( this.output );
		LobbyCameraMode.GateAnimationRequested += ( _, entering ) => { if ( entering ) opened++; else closed++; };
	}
	private void Next( int value ) { stage = value; changed = Time.Now; }
	/// <summary>Points the simulated mouse at a position in the 2048x1536 retail UI units.</summary>
	private static void Hover( float x, float y ) => Input.PointerOverride = new Vector2( x * Screen.Size.X / 2048, y * Screen.Size.Y / 1536 );
	private void Check( bool value, string description )
	{
		if ( !value ) throw new InvalidOperationException( $"Lobby acceptance failed: {description}" );
		checks.Add( description ); Log.Info( $"Lobby acceptance: {description}" );
	}
	internal void Update()
	{
		if ( Time.Now - beginning > 180 ) throw new TimeoutException( $"Lobby acceptance timed out at stage {stage}" );
		double elapsed = Time.Now - changed;
		var navigation = level.Navigation!;
		switch ( stage )
		{
			case 0 when elapsed > 0.5:
				// Read an existing local profile. Acceptance must never create or overwrite player data.
				int slot = Array.FindIndex( LobbyPlayerProfiles.Read(), profile => profile != null );
				if ( slot < 0 ) throw new InvalidOperationException( "Lobby acceptance needs an existing test profile" );
				level.PlayerMenu!.Select( slot );
				Check( navigation.Active && level.IslandMenu!.Active, "existing profile opens island page" );
				Next( navigation.InstantAction ? 40 : 1 ); break;
			case 40 when elapsed > 3:
				// US 0x4b9340: GameType 2 (Instant Action) fixes the island, hides keys and allows entry without them.
				int island = navigation.Island.Id;
				navigation.Navigate( 1 ); navigation.Navigate( -1 );
				Check( navigation.Island.Id == island && navigation.CanEnter, "Instant Action keeps the island fixed and enterable" );
				Capture( "lobby-instant-island" ); Hover( (68 + 221) / 2f, (1114 + 1267) / 2f ); Next( 41 ); break;
			case 41 when elapsed > 0.3:
				Capture( "lobby-island-help" );
				Check( level.Help?.Shown == 345, "hovering the island enter button shows UIHELPTEXT 345" );
				Hover( (97 + 250) / 2f, (1299 + 1453) / 2f ); Next( 42 ); break;
			case 42 when elapsed > 0.3:
				Check( level.Help?.Shown == -1, "a disabled Instant Action arrow shows no help" );
				Input.PointerOverride = null;
				// Continue the key and navigation checks with an in-memory Full Simulation copy; nothing is written.
				var simulation = PlayerProfileFile.Read( level.PlayerMenu!.Profile!.Write() );
				simulation.Prefix[14] = 0;
				navigation.Bind( simulation ); level.IslandMenu!.Activate();
				Check( !navigation.InstantAction, "Full Simulation copy restores key gates" );
				Next( 1 ); break;
			case 1 when elapsed > 3:
				Capture( $"lobby-{navigation.Island.ThemeName}" );
				if ( !navigation.CanEnter ) Check( !navigation.Enter(), $"locked {navigation.Island.ThemeName} rejects entry" );
				navigation.Navigate( 1 );
				if ( ++visited < 4 ) Next( 1 );
				else { Check( navigation.Island.Id == level.Lobby!.Islands[0].Id, "four-island navigation wraps" ); Next( 2 ); }
				break;
			case 2 when elapsed > 1:
				// Enter through the real pointer path: hover the gate button (0x1e0e8) and click it.
				Check( navigation.CanEnter, "affordable island can be entered" );
				Hover( (68 + 221) / 2f, (1114 + 1267) / 2f ); Input.ClickOverride = true; Next( 34 ); break;
			case 34 when elapsed > 0.3:
				Input.PointerOverride = null;
				Check( navigation.Entering, "clicking the island gate button starts entry" );
				Check( navigation.CancelEntry() && opened == 0 && closed == 0 &&
					LobbyCameraMode.Movement == LobbyCameraState.Movement.Orbiting, "alignment cancellation leaves the gate closed" );
				Next( 3 ); break;
			case 3 when elapsed > 1:
				// Retail also enters when a left press lands on the scene rather than a page element (0x5e2310).
				// The title text element (0x1e0ef) takes the press, so it must not start entry.
				Hover( 1024, 1370 ); Input.ClickOverride = true; Next( 36 ); break;
			case 36 when elapsed > 0.3:
				Check( !navigation.Entering, "clicking the island title does not enter" );
				// Inside the panel's bounding box but outside its L-shaped hit outline: the lobby view gets the press.
				Hover( 400, 1000 ); Input.ClickOverride = true; Next( 35 ); break;
			case 35 when elapsed > 0.3:
				Input.PointerOverride = null;
				Check( navigation.Entering, "clicking the scene beside the panel outline restarts entry" ); Next( 4 ); break;
			case 4 when LobbyCameraMode.Movement == LobbyCameraState.Movement.Entering:
				// Cancel in the update that actually dispatched M1, while its non-looping segment is unfinished.
				Check( opened == 1 && navigation.CancelEntry() && closed == 1, "opened gate dispatches one close request" );
				cancellingGate = Entity.All.OfType<LobbyGate>().Single( gate => gate.HasPendingAnimation );
				Check( cancellingGate.HasPendingAnimation, "M2 queues behind the active M1 segment" );
				Next( 5 ); break;
			case 5 when cancellingGate!.AnimationFinished && !cancellingGate.HasPendingAnimation:
				Check( !navigation.Entering && LobbyCameraMode.EntryFade == 0, "queued closure finishes and clears entry fade" );
				Capture( "lobby-gate-cancelled" ); level.GlobeMenu!.Show( LobbyGlobeMode.Visit ); Next( 6 ); break;
			case 6 when elapsed > 1:
				Check( level.GlobeMenu!.Active, "globe opens offline" ); Capture( "lobby-globe-offline" );
				level.GlobeMenu.CameraState.ToggleZoom(); level.GlobeMenu.CameraState.Turn( 1, 1 ); Next( 7 ); break;
			case 7 when elapsed > 1:
				Check( level.GlobeMenu!.CameraState.Zoomed, "globe zoom toggles" ); Capture( "lobby-globe-zoom" );
				level.GlobeMenu.Hide(); Next( 8 ); break;
			case 8 when elapsed > 1:
				Check( !level.GlobeMenu!.Active && level.IslandMenu!.Active, "globe return restores island page" );
				Capture( "lobby-globe-return" );
				level.HandleEscape();
				Check( level.Menu!.Active && level.Hud.Modal == level.Menu, "orbit Escape opens the shared local menu" );
				Next( 9 ); break;
			case 9 when elapsed > 0.5:
				// The Escape menu items are created at run time and never set element help (US 0x48c600, 0x492f60).
				Capture( "lobby-menu" ); Hover( 1024, 30 ); Next( 32 ); break;
			case 32 when elapsed > 0.3:
				Capture( "lobby-menu-hover" );
				Check( level.Help?.Shown == -1, "hovering an Escape menu item shows no help" );
				Input.PointerOverride = null; level.Menu!.Choose( UIStrings.QuitGame ); Next( 15 ); break;
			case 15 when elapsed > 0.5:
				// Confirmation table US 0x74f920: every element has help -1.
				Check( Confirmation.MessageFits, "quit confirmation text wraps inside its box" );
				Capture( "lobby-quit-cancel" ); Hover( (1379 + 1462) / 2f, (522 + 605) / 2f ); Next( 33 ); break;
			case 33 when elapsed > 0.3:
				Capture( "lobby-quit-hover" );
				Check( level.Help?.Shown == -1, "hovering the confirmation OK shows no help" );
				Input.PointerOverride = null; Confirmation.Close();
				Check( level.Menu!.Active && level.Hud.Modal == level.Menu, "quit cancellation restores menu input" );
				level.Menu.Choose( UIStrings.ResumeGame );
				Check( !level.Menu.Active && level.Hud.Modal == null && navigation.Active, "resume leaves the selected player and island intact" );
				level.HandleEscape(); level.Menu.Choose( UIStrings.SelectNewPlayer ); Confirmation.Accept();
				Check( level.PlayerMenu!.Browsing && !navigation.Active && !level.IslandMenu!.Active &&
					level.Hud.Modal == null, "confirmed new-player selection returns to profiles" );
				Next( 10 ); break;
			case 10 when elapsed > 0.5:
				// Player page table US 0x753c60: slot and Quit elements have help -1.
				Capture( "lobby-player" ); Hover( (884 + 2048) / 2f, (46 + 193) / 2f ); Next( 31 ); break;
			case 31 when elapsed > 0.3:
				Capture( "lobby-player-hover" );
				Check( level.Help?.Shown == -1, "hovering a player slot shows no help" );
				Input.PointerOverride = null;
				// An empty slot exercises the real naming dialog without publishing a new profile.
				int empty = Array.FindIndex( LobbyPlayerProfiles.Read(), profile => profile == null );
				if ( empty < 0 ) throw new InvalidOperationException( "Naming acceptance needs an empty test slot" );
				level.PlayerMenu!.Select( empty ); Next( 11 ); break;
			case 11 when elapsed > 0.5:
				Capture( "lobby-naming" );
				// Hover OK through the input snapshot: help 360 and its MessageTag1 (0x48b9f0).
				Input.PointerOverride = new Vector2( (1376 + 1460) / 2f * Screen.Size.X / 2048, (524 + 608) / 2f * Screen.Size.Y / 1536 );
				Next( 30 ); break;
			case 30 when elapsed > 0.3:
				Capture( "lobby-naming-help" );
				Check( level.Help?.Shown == 360, "hovering OK shows UIHELPTEXT 360" );
				Input.PointerOverride = null; level.PlayerMenu!.Cancel();
				Check( level.Help?.Shown == -1, "closing the naming dialog hides the help strip" );
				Check( level.PlayerMenu.Browsing, "naming cancellation returns to profile selection without creating data" ); Next( 12 ); break;
			case 12 when elapsed > 0.5:
				level.HandleEscape(); level.Menu!.Choose( UIStrings.QuitGame ); Next( 13 ); break;
			case 13 when elapsed > 0.5:
				Capture( "lobby-quit-accept" ); Confirmation.Accept(); Next( 14 ); break;
		}
	}
	internal void VerifyCompleted()
	{
		Check( stage == 14 && !Window.Current.SdlWindow.Exists, "confirmed quit closes without rendering another frame" );
		File.WriteAllText( Path.Join( output, "acceptance.json" ), JsonSerializer.Serialize( new { passed = true,
			assembly = typeof(LobbyAcceptance).Assembly.ManifestModule.ModuleVersionId,
			width = Render.ResolveColorTexture.Width, height = Render.ResolveColorTexture.Height, checks },
			new JsonSerializerOptions { WriteIndented = true } ) );
		Log.Info( "Lobby acceptance PASS" );
	}
	// Reads only the game's resolved render target, never the desktop or another application.
	private void Capture( string name ) => FrameCapture.SaveBmp( Path.Join( output, name + "_opentpw.bmp" ) );
}
#endif
