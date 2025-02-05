using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

internal class PurpleButton : Panel
{
	private Texture ButtonText1;
	private Texture ButtonText2;

	public string Text { get; set; }

	public PurpleButton( string text )
	{
		ButtonText1 = new Texture( "ui/textures/button_text1.wct", TextureFlags.PointFilter );
		ButtonText2 = new Texture( "ui/textures/button_text2.wct", TextureFlags.PointFilter );

		Text = Localization.Parse( text );
		Log.Info( $"Parsed '{text}' as '{Text}'" );
	}

	protected override void OnRender()
	{
		var material = Material.UI;
		var size = new Vector2( 200, 100 );
		var rect = new Rectangle( position, size );

		var uvs = new Rectangle( 0f, 0f, 1.0f, 0.5f );
		rect.Y -= 130;
		rect.X -= 740;

		using ( var ctx = new ResourceContext() )
		{
			//
			// End
			//
			ctx.Set( "Color", ButtonText1 );
			rect.X += 190;
			Graphics.Quad( rect, uvs.Shift( new Vector2( 0, 0.50f ) ), material );

			//
			// Middle
			//
			ctx.Set( "Color", ButtonText1 );
			rect.X += 190;
			Graphics.Quad( rect.Shift( new Vector2( 0, 28 ) ), uvs.Shift( new Vector2( 0, 0f ) ), material );

			//
			// Start
			//
			rect.X += 200;
			ctx.Set( "Color", ButtonText2 );
			Graphics.Quad( rect, uvs.Shift( new Vector2( 0f, 0.50f ) ), material );
		}
	}
}
