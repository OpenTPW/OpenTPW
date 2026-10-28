using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// The hover help strip at the bottom of the screen (US TP.exe 0x492180, global instance 0x7ca0c0): an
/// <c>f_helpbg</c> model behind white font-slot-7 text, x 574–1474 and ending at y 1520 in UI units. Its height
/// is two text lines plus 10 units ((line height × 3 &lt;&lt; 10) / screen height + 10). 0x4924b0 sets the text;
/// an empty string hides the strip. Texts come from UIHELPTEXT.str, numbered by each UI-table element's key 0x11.
/// </summary>
internal sealed class LobbyHelpBar : Panel
{
	private static StringFile? texts;
	private readonly LobbyUiModel background;
	private readonly LobbyUiText text;
	private int shown = -1;

	public LobbyHelpBar()
	{
		text = new LobbyUiText( "", new Rectangle(), LobbyFonts.Name( 7 ) );
		float height = text.LineHeight * 3.2f * 2 + 10;
		var bounds = LobbyNamingPanel.Box( 574, 1520 - height, 1474, 1520 );
		background = new LobbyUiModel( "f_helpbg", bounds );
		text = new LobbyUiText( "", bounds, LobbyFonts.Name( 7 ) ) { WordWrap = true };
	}

	/// <summary>UIHELPTEXT entry currently shown, or −1 when hidden.</summary>
	public int Shown => shown;

	internal static string Text( int number )
	{
		texts ??= new StringFile( GameLanguage.Path( "UIHELPTEXT.str" ) );
		return number >= 0 && number < texts.Entries.Length ? texts[number]
			: throw new ArgumentOutOfRangeException( nameof(number), $"UIHELPTEXT has no entry {number}" );
	}

	/// <summary>Shows a UIHELPTEXT entry; −1 hides the strip.</summary>
	public void Show( int number )
	{
		if ( number == shown ) return;
		shown = number;
		text.Text = number < 0 ? "" : Text( number );
	}

	protected override void OnRender()
	{
		if ( shown < 0 ) return;
		background.Draw();
		text.Draw();
	}
}
