using Veldrid;

namespace OpenTPW.UI;

/// <summary>A label using the original game's font profiles and MSDF rendering.</summary>
public class Label : Panel
{
	public string Text { get; set; }
	public GameFont Font { get; set; } = GameFont.GameMedium;
	public float Scale { get; set; } = 1;
	public TextAlignment Alignment { get; set; }
	public RgbaFloat Color { get; set; } = RgbaFloat.White;

	public Label( string text ) => Text = Localization.Parse( text );
	protected override void OnRender() => MsdfText.Draw( Text, Position, Font, Color, Scale, Alignment );
}
