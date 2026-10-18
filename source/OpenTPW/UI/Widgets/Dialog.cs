using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

internal class Dialog : Panel
{
	public override bool IsModal => true;

	private readonly Model window;
	private static Texture? panelSurface;
	private readonly Texture overlay;
	private readonly Action? confirm;
	private readonly string heading;
	private readonly string? message;
	private bool armedConfirm;
	private bool armedCancel;
	private readonly ButtonHoverEffect confirmHover = new();
	private readonly ButtonHoverEffect cancelHover = new();
	protected const float Width = 680;
	protected const float Height = 310;

	protected float Scale => Math.Min( 1f, Math.Min( Screen.RawSize.X / 760f, Screen.RawSize.Y / 360f ) );

	protected Vector2 Origin => (Screen.RawSize - new Vector2( Width, Height ) * Scale) / 2;

	protected Rectangle Rect( float x, float y, float w, float h ) => new( Origin + new Vector2( x, y ) * Scale, new Vector2( w, h ) * Scale );

	public bool IsWave { get; set; } = false;

	public Dialog( string title, Action? confirm = null )
	{
		overlay = UiAssets.GetTexture( "lolight" );
		panelSurface ??= new Texture( new byte[] { 37, 43, 92, 255 }, 1, 1 );
		var lines = Localization.Parse( title ).Split( "\n\n", 2 );
		heading = lines[0];
		message = lines.Length > 1 ? lines[1] : null;
		this.confirm = confirm;
		// Dialogs use their authored X/Y geometry in screen space.
		string asset = IsWave ? "w_dialog_wave" : "w_dialog";
		window = UiAssets.GetModel( asset, new Vector2( Width, Height ) );
	}

	protected void Text( string text,
		float x,
		float y,
		float size = 1,
		TextAlignment alignment = TextAlignment.Left,
		GameFont? font = null,
		RgbaFloat? color = null ) => MsdfText.Draw( text, Origin + new Vector2( x, y ) * Scale, font ?? GameFont.GameBold, color ?? RgbaFloat.White, Scale * size, alignment );

	protected void Sprite( string asset, Rectangle rect ) => UiDrawing.Sprite( asset, rect );

	// Keep the original rounded corners and bevels intact when stretching frames.
	protected void Frame( string asset, Rectangle rect, float sourceBorder, float border )
	{
		var texture = UiAssets.GetTexture( asset );
		float[] x = [rect.X, rect.X + border * Scale, rect.X + rect.Width - border * Scale, rect.X + rect.Width];
		float[] y = [rect.Y, rect.Y + border * Scale, rect.Y + rect.Height - border * Scale, rect.Y + rect.Height];
		float[] u = [0, sourceBorder / texture.Width, 1 - sourceBorder / texture.Width, 1];
		float[] v = [0, sourceBorder / texture.Height, 1 - sourceBorder / texture.Height, 1];
		using var context = new ResourceContext();
		context.Set( "Color", texture );
		for ( int row = 0; row < 3; row++ )
		{
			for ( int col = 0; col < 3; col++ )
			{
				Graphics.Quad( new Rectangle( x[col], y[row], x[col + 1] - x[col], y[row + 1] - y[row] ),
					new Rectangle( u[col], v[row], u[col + 1] - u[col], v[row + 1] - v[row] ),
					Material.UIBasic );
			}
		}
	}

	protected void ModePanelSurface( Rectangle rect )
	{
		UiDrawing.Sprite( panelSurface!, rect );
	}

	private Rectangle ConfirmRect => Rect( 586, 151, 56, 47 );

	private Rectangle CancelRect => Rect( 592, 198, 56, 47 );

	protected override void OnRender()
	{
		UiDrawing.Sprite( overlay, new Rectangle( Vector2.Zero, Screen.RawSize ) );
		UiDrawing.Model( window, Origin, new Vector2( Scale ) );

		var headingFont = GameFont.GameBold;
		float headingScale = Math.Min( 1.4f, 485f / Math.Max( 1, MsdfText.Measure( heading, headingFont ).X ) );
		Text( heading, 323, 34, headingScale, TextAlignment.Center, headingFont, new RgbaFloat( .1f, .1f, .1f, 0.1f ) );
		Text( heading, 325, 32, headingScale, TextAlignment.Center, headingFont, new RgbaFloat( .95f, .91f, .34f, 1 ) );
		if ( message != null )
		{
			Text( message, 340, 130, 1, TextAlignment.Center );
		}

		RenderContents();
		Sprite( Input.Mouse.Left && armedConfirm ? "b_okayd" : "b_okay", ConfirmRect );
		Sprite( Input.Mouse.Left && armedCancel ? "b_exitd" : "b_exit", CancelRect );
		confirmHover.Draw( ConfirmRect, CanReceiveInput && ConfirmRect.Contains( Input.Mouse.Position ), Scale );
		cancelHover.Draw( CancelRect, CanReceiveInput && CancelRect.Contains( Input.Mouse.Position ), Scale );
	}

	protected virtual void RenderContents()
	{
	}

	protected virtual void Confirm()
	{
		confirm?.Invoke();
		Delete();
	}

	protected override void OnUpdate()
	{
		if ( Input.KeyPressed( Key.Escape ) )
		{
			Delete();
			return;
		}

		if ( Input.KeyPressed( Key.Enter ) )
		{
			Confirm();
			return;
		}

		if ( Input.LeftPressed )
		{
			armedConfirm = ConfirmRect.Contains( Input.Mouse.Position );
			armedCancel = CancelRect.Contains( Input.Mouse.Position );
		}

		if ( Input.LeftReleased )
		{
			bool ok = armedConfirm && ConfirmRect.Contains( Input.Mouse.Position );
			bool cancel = armedCancel && CancelRect.Contains( Input.Mouse.Position );
			armedConfirm = armedCancel = false;
			if ( cancel )
			{
				Delete();
			}
			else if ( ok )
			{
				Confirm();
			}
		}
	}
}
