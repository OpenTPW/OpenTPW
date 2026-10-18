using OpenTPW.UI;

namespace OpenTPW;

internal class PurpleButton : Panel
{
	private readonly Texture buttonText1;
	private readonly Texture buttonText2;
	private bool armed;

	public string Text { get; set; }
	public Action? Clicked { get; set; }

	public Rectangle Bounds => new Rectangle( Position.X - 740, Position.Y + 28, 590, 100 );

	public PurpleButton( string text )
	{
		buttonText1 = UiAssets.GetTexture( "button_text1", TextureFlags.PointFilter );
		buttonText2 = UiAssets.GetTexture( "button_text2", TextureFlags.PointFilter );
		Text = Localization.Parse( text );
	}

	protected override void OnRender()
	{
		var size = new Vector2( 200, 100 );
		var topHalf = new Rectangle( 0, 0, 1, .5f );
		var bottomHalf = new Rectangle( 0, .5f, 1, .5f );
		UiDrawing.Sprite( buttonText1, new Rectangle( Position + new Vector2( -350, 0 ), size ), bottomHalf );
		UiDrawing.Sprite( buttonText1, new Rectangle( Position + new Vector2( -540, 28 ), size ), topHalf );
		UiDrawing.Sprite( buttonText2, new Rectangle( Position + new Vector2( -150, 28 ), size ), topHalf );

		var hovered = Bounds.Contains( Input.Mouse.Position );
		var color = hovered ? new Veldrid.RgbaFloat( 1.0f, 0.8f, 0.1f, 1.0f ) : new Veldrid.RgbaFloat( 0.92549f, 0.63529f, 0.01569f, 1.0f );
		var font = GameFont.GameBold;
		float textScale = Math.Min( 1f, 500f / Math.Max( MsdfText.Measure( Text, font ).X, 1 ) );
		var textPosition = Position + new Vector2( -350, 48 );
		MsdfText.Draw( Text, textPosition, font, color, textScale, TextAlignment.Center );
	}

	protected override void OnUpdate()
	{
		if ( Input.LeftPressed )
		{
			armed = Bounds.Contains( Input.Mouse.Position );
		}

		if ( Input.LeftReleased )
		{
			bool clicked = armed && Bounds.Contains( Input.Mouse.Position );
			armed = false;
			if ( clicked )
			{
				Clicked?.Invoke();
			}
		}
	}
}
