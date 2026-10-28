using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

/// <summary>
/// Player page: four slot buttons, Quit and the naming dialog, driving <see cref="LobbyPlayerFlow"/> and <see cref="LobbyPlayerStore"/>.
/// Rectangles are the retail UI table values (0x74f770) in 2048x1536 units.
/// This page shows no hover help: every element of the US table (0x753c60–0x753f68, slots 31252–31259 and Quit 31260)
/// has key 0x11 (and 0x12) = −1, so <see cref="LobbyHelpBar"/> stays hidden here.
/// </summary>
internal sealed class LobbyPlayerPanel : Panel
{
	private readonly LobbyPlayerStore store;
	private readonly LobbyPlayerFlow flow;
	private readonly Level level;
	private readonly Image logo;
	private readonly PurpleButton[] players = new PurpleButton[4];
	private readonly PurpleButton quit;
	private readonly LobbyNamingPanel naming;
	private readonly LobbyUiText failure = new( "", LobbyNamingPanel.Box( 500, 1100, 2000, 1250 ) );
	private int focused;
	/// <summary>Raised after the profile was read or created on disk; the island page takes over.</summary>
	public event Action<LobbyPlayerProfile, PlayerProfileFile>? PlayerSelected;
	public event Action? QuitRequested;
	public bool Browsing => flow.State == LobbyPlayerFlowState.Browsing;
	public PlayerProfileFile? Profile { get; private set; }

	private static Rectangle Box( float left, float top, float right, float bottom ) => LobbyNamingPanel.Box( left, top, right, bottom );
	private static Rectangle Pixels( Rectangle units ) => new( units.X / 3.2f, units.Y / 3.2f, units.Width / 3.2f, units.Height / 3.2f );
	public LobbyPlayerPanel( Level level )
	{
		this.level = level;
		store = new LobbyPlayerStore( SaveFileSystem.GetAbsolutePath( "users" ), FileSystem, level.Lobby!.Islands.Select( island => island.ThemeName ) );
		flow = new LobbyPlayerFlow( store.ReadSlots() );
		logo = new Image( "/ui/textures/tpw_logo.wct" ) { Uv0 = new( 0, 0.6f ), Uv1 = new( 1, 1 ), Position = new( 25 ), Size = new( 180, 77.4f ) };
		RefreshButtons();
		quit = new PurpleButton( "#QuitGame", Pixels( Box( 1264, 895, 2428, 1042 ) ), Pixels( Box( 1339, 924, 2039, 1013 ) ) );
		naming = new LobbyNamingPanel( flow, Confirm, Cancel );
	}
	private void RefreshButtons()
	{
		for ( int i = 0; i < players.Length; i++ )
		{
			float step = i * 199;
			players[i] = new PurpleButton( flow.SlotLabel( i ) ?? "#CreateNewPlayer", Pixels( Box( 884, 46 + step, 2048, 193 + step ) ), Pixels( Box( 941, 75 + step, 1716, 164 + step ) ) );
		}
	}
	internal void Select( int slot )
	{
		if ( !flow.Select( slot ) ) return;
		failure.Text = "";
		if ( flow.State == LobbyPlayerFlowState.Naming )
		{
			level.Advisor?.Say( 0x23a, true );
			naming.ShowError( "" );
			naming.Open();
			return;
		}
		try { Commit( flow[slot]! ); }
		catch ( Exception error ) when ( error is IOException or UnauthorizedAccessException )
		{
			flow.Back();
			failure.Text = error.Message;
			Log.Warning( $"Unable to load player: {error.Message}" );
		}
	}
	private void Confirm( byte mode )
	{
		if ( !flow.CanConfirm ) return;
		// Publish a complete profile before exposing the occupied slot or changing pages.
		try
		{
			var profile = store.Create( flow.ActiveSlot, flow.ResolveName(), mode, LobbyOptionsSettings.Current.Preferences );
			flow.Confirm();
			flow.Adopt( profile );
			Commit( profile );
			RefreshButtons();
			naming.Close();
		}
		catch ( Exception error ) when ( error is IOException or UnauthorizedAccessException )
		{
			if ( flow.State == LobbyPlayerFlowState.Committed )
			{
				// A published profile remains a valid occupied slot even if binding its next page fails.
				flow.Reload( store.ReadSlots() );
				RefreshButtons();
				naming.Close();
				failure.Text = error.Message;
			}
			else naming.ShowError( error.Message );
			Log.Warning( $"Unable to create player: {error.Message}" );
		}
	}
	private void Commit( LobbyPlayerProfile profile )
	{
		// Apply the selected player's sound preferences before queuing the new-player speech.
		var data = store.Load( profile );
		level.Audio?.ConfigurePreferences( data.Preferences );
		Profile = data;
		level.Advisor?.Cancel();
		if ( level.Advisor != null ) level.Advisor.Enabled = data.Preferences[32] != 0;
		if ( flow.Result?.Kind == LobbyPlayerChoiceKind.New ) level.Advisor?.Say( 0x189 );
		PlayerSelected?.Invoke( profile, data );
	}
	internal void Cancel()
	{
		flow.Cancel();
		naming.Close();
		level.Advisor?.Cancel();
	}
	public void Back()
	{
		flow.Back();
		Profile = null;
	}
	protected override void OnUpdate()
	{
		// The naming dialog consumes this page's input until it confirms or cancels.
		if ( naming.Active ) { naming.Update(); return; }
		if ( !Browsing ) return;
		if ( Input.Pressed( Key.Up ) ) focused = (focused + 4) % 5;
		if ( Input.Pressed( Key.Down ) ) focused = (focused + 1) % 5;
		if ( Input.Pressed( Key.Enter ) )
		{
			if ( focused == 4 ) QuitRequested?.Invoke();
			else Select( focused );
			return;
		}
		if ( !Input.LeftPressed ) return;
		for ( int i = 0; i < 4; i++ )
		{
			if ( !LobbyNamingPanel.Hit( Box( 884, 46 + i * 199, 2048, 193 + i * 199 ) ) ) continue;
			focused = i;
			Select( i );
			return;
		}
		if ( LobbyNamingPanel.Hit( Box( 1264, 895, 2428, 1042 ) ) ) QuitRequested?.Invoke();
	}
	protected override void OnRender()
	{
		if ( flow.State == LobbyPlayerFlowState.Committed ) return;
		logo.Draw();
		foreach ( var player in players ) player.Draw();
		quit.Draw();
		failure.Draw();
		naming.Draw();
	}
}
