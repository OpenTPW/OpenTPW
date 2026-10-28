using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

/// <summary>
/// Two-button message dialog, table 0x74b468 and 0x47d290. The US table (0x74f920) gives every element key 0x11 (and
/// 0x12) = −1, so hovering OK or Cancel shows no help.
/// </summary>
internal sealed class LobbyConfirmPanel : Panel
{
	private readonly RootPanel hud;
	private readonly LobbyUiModel backdrop, background, cancel, okay;
	private readonly LobbyUiText message;
	private Action? confirm;
	private Panel? previousModal;
	private ulong activatedFrame;
	private static readonly Rectangle CancelBox = LobbyNamingPanel.Box( 1387, 610, 1470, 694 ),
		OkayBox = LobbyNamingPanel.Box( 1379, 522, 1462, 605 );
	public bool Active => confirm != null;
	/// <summary>Whether the wrapped message fits the 735×477-unit text rectangle (acceptance check).</summary>
	internal bool MessageFits
	{
		get
		{
			var lines = message.Lines( message.Text );
			return lines.All( line => message.Measure( line ) <= (1345 - 610) / 3.2f ) && lines.Length * message.LineHeight <= (753 - 276) / 3.2f;
		}
	}
	public LobbyConfirmPanel( RootPanel hud )
	{
		this.hud = hud;
		backdrop = new LobbyUiModel( "LOLIGHT", LobbyNamingPanel.Box( 0, 0, 2047, 1535 ) );
		background = new LobbyUiModel( LobbyControlResources.ResolveModel( 0xd8cca935 ), LobbyNamingPanel.Box( 500, 233, 1516, 830 ) );
		cancel = new LobbyUiModel( "b_exit", CancelBox );
		okay = new LobbyUiModel( "b_okay", OkayBox );
		message = new LobbyUiText( "", LobbyNamingPanel.Box( 610, 276, 1345, 753 ), LobbyFonts.Name( 4 ) ) { WordWrap = true };
	}
	public void Show( string text, Action accepted )
	{
		// Ignore the opening click for one frame and give this dialog exclusive input routing.
		message.Text = text; confirm = accepted; activatedFrame = Input.FrameNumber;
		previousModal = hud.Modal;
		hud.Modal = this;
	}
	internal void Close()
	{
		// A confirmation opened by the Escape menu returns input to that menu on cancellation.
		confirm = null;
		if ( hud.Modal == this ) hud.Modal = previousModal;
		previousModal = null;
	}
	internal void Accept()
	{
		// Capture the action before clearing modal ownership; a callback may destroy this window.
		var accepted = confirm ?? throw new InvalidOperationException( "No active confirmation" );
		Close(); accepted();
	}
	protected override void OnUpdate()
	{
		if ( !Active || activatedFrame == Input.FrameNumber ) return;
		int State( Rectangle bounds ) => LobbyNamingPanel.Hit( bounds ) ? Input.Mouse.Left ? 3 : 2 : 0;
		cancel.State = State( CancelBox ); okay.State = State( OkayBox );
		if ( Input.Pressed( Key.Escape ) || Input.LeftPressed && LobbyNamingPanel.Hit( CancelBox ) ) { Close(); return; }
		if ( Input.Pressed( Key.Enter ) || Input.LeftPressed && LobbyNamingPanel.Hit( OkayBox ) )
		{
			// Release modal ownership before running callbacks that may close the game window.
			Accept();
		}
	}
	protected override void OnRender()
	{
		if ( !Active ) return;
		backdrop.Draw(); background.Draw(); cancel.Draw(); okay.Draw(); message.Draw();
	}
}
