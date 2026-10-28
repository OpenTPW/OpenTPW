using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

/// <summary>
/// Local actions and Options from the shared Escape menu (US 0x48c600, 0x48bd40). The items are run-time text
/// elements added by 0x492f60, which sets no help number, so they keep the element default −1 written by the base
/// constructor (US 0x65bdea, +0x90): no hover help.
/// </summary>
internal sealed class LobbyMenuPanel : Panel
{
	private readonly Level level;
	private readonly RootPanel hud;
	private readonly LobbyConfirmPanel confirmation;
	private readonly LobbyUiModel backdrop;
	private List<(UIStrings Action, LobbyUiText Text, Rectangle Bounds)> items = new();
	private readonly Dictionary<bool, List<(UIStrings Action, LobbyUiText Text, Rectangle Bounds)>> layouts = new();
	private ulong activatedFrame;
	private int focused;
	public bool Active { get; private set; }
	internal event Action? OptionsRequested;
	internal LobbyMenuPanel( Level level, LobbyConfirmPanel confirmation )
	{
		this.level = level; hud = level.Hud; this.confirmation = confirmation;
		// US 0x492e80 creates the full-screen lolight underlay before its text children.
		backdrop = new LobbyUiModel( "LOLIGHT", LobbyNamingPanel.Box( 0, 0, 2047, 1535 ) );
		backdrop.PreloadAll();
		layouts.Add( false, BuildLayout( false ) ); layouts.Add( true, BuildLayout( true ) );
	}
	internal void Show()
	{
		if ( Active || hud.Modal != null ) return;
		focused = 0;
		bool selected = level.Navigation!.Active;
		items = layouts[selected];
		Active = true; activatedFrame = Input.FrameNumber; hud.Modal = this;
	}
	private List<(UIStrings Action, LobbyUiText Text, Rectangle Bounds)> BuildLayout( bool selected )
	{
		var result = new List<(UIStrings Action, LobbyUiText Text, Rectangle Bounds)>();
		float top = 5;
		void Add( UIStrings action )
		{
			var label = new LobbyUiText( Localization.Text( action ), new Rectangle( 0, 0, 2048, 1536 ), LobbyFonts.Name( 0 ) );
			// US 0x492f60 centres measured font-0 text, starting at y=5 with a five-unit row gap.
			float width = label.Measure( label.Text ) * 3.2f, height = (label.LineHeight + 5) * 3.2f;
			var bounds = new Rectangle( (2048 - width) / 2, top, width, height );
			// US 0x48b2a0 handles message 0x11 by making every menu item opaque cyan.
			var text = new LobbyUiText( label.Text, bounds, LobbyFonts.Name( 0 ), 0x00afbeff );
			text.Preload( new[] { text.Text } );
			result.Add( (action, text, bounds) ); top += height + 5;
		}
		// US 0x48c600 adds Options (UITEXT 6) before SelectNewPlayer (13), Resume (7), Quit (8).
		// Online actions remain dependent on the deferred city service.
		Add( UIStrings.Options );
		if ( selected ) Add( UIStrings.SelectNewPlayer );
		Add( UIStrings.ResumeGame ); Add( UIStrings.QuitGame );
		return result;
	}

	internal void Close()
	{
		Active = false;
		if ( hud.Modal == this ) hud.Modal = null;
	}
	internal void Choose( UIStrings action )
	{
		if ( !Active || !items.Any( item => item.Action == action ) ) throw new InvalidOperationException( "Inactive lobby menu action" );
		if ( action == UIStrings.ResumeGame ) { Close(); return; }
		// US 0x48bd40 closes the Escape menu before constructing the options page.
		if ( action == UIStrings.Options ) { Close(); OptionsRequested?.Invoke(); return; }
		// TW 0x489fff uses ConfirmNewPlayer; 0x48a109 uses ConfirmQuit. Cancellation keeps the menu.
		confirmation.Show( Localization.Parse( action == UIStrings.SelectNewPlayer ? "#ConfirmNewPlayer" : "#ConfirmQuit" ), () =>
		{
			Close();
			if ( action == UIStrings.SelectNewPlayer ) level.ReturnToPlayers();
			else Window.Current.SdlWindow.Close();
		} );
	}
	protected override void OnUpdate()
	{
		if ( !Active || activatedFrame == Input.FrameNumber ) return;
		if ( Input.Pressed( Key.Escape ) ) { Close(); return; }
		if ( Input.Pressed( Key.Up ) ) focused = (focused + items.Count - 1) % items.Count;
		if ( Input.Pressed( Key.Down ) ) focused = (focused + 1) % items.Count;
		for ( int i = 0; i < items.Count; i++ )
			if ( LobbyNamingPanel.Hit( items[i].Bounds ) )
			{
				focused = i;
				if ( Input.LeftPressed ) { Choose( items[i].Action ); return; }
			}
		if ( Input.Pressed( Key.Enter ) ) Choose( items[focused].Action );
	}
	protected override void OnRender()
	{
		if ( !Active ) return;
		backdrop.Draw();
		foreach ( var item in items ) item.Text.Draw();
	}
}
