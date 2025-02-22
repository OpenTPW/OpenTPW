namespace OpenTPW;

public static class LoadingScreen
{
	private const float ShadowPadding = 64f;
	private const float ProgressBarHorizontalOffset = 22f;
	private const float ProgressBarVerticalOffset = 56f;
	private const float ProgressBarMaxWidth = 512f;
	private const float ProgressBarHeight = 20f;

	private static Texture BackgroundTexture;
	private static Texture ShadowTexture;
	private static Texture LoadingTexture;
	private static Texture ProgressTexture;
	private static Color BackgroundColor = Color.Parse( "#0a2f72" );

	/// <summary>
	/// Show loading screen?
	/// </summary>
	public static bool Visible { get; set; }

	/// <summary>
	/// 0 to 1
	/// </summary>
	public static float Progress { get; set; }

	static LoadingScreen()
	{
		ProgressTexture = CreateProgressTexture();
		LoadingTexture = new Texture( GameDir.GetPath( "data/Init/1024/Welcome.tga" ) );
		ShadowTexture = new Texture( "content/textures/shadow.png" );
		BackgroundTexture = CreateBackgroundTexture();
	}

	public static void RenderUI()
	{
		if ( !Visible )
			return;

		using var _ = new Graphics.Scope( (Point2)Screen.Size );

		DrawBackground();
		DrawShadow();
		DrawLoading();
		DrawProgress();
	}

	private static Texture CreateProgressTexture()
	{
		return new Texture(
			[
				150, 10, 0, 255,
				255, 25, 0, 255,
				255, 25, 0, 255,
				255, 25, 0, 255,
				150, 10, 0, 255
			],
			1, 5,
			TextureFlags.PointFilter
		);
	}

	private static Texture CreateBackgroundTexture()
	{
		return new Texture(
			[(byte)BackgroundColor.R, (byte)BackgroundColor.G, (byte)BackgroundColor.B, (byte)BackgroundColor.A],
			1, 1
		);
	}

	private static void DrawBackground()
	{
		Material.UIBasic.Set( "Color", BackgroundTexture );
		Graphics.Quad( new Rectangle( Vector2.Zero, Screen.Size ), Material.UIBasic );
	}

	private static void DrawShadow()
	{
		var shadowRect = GetTextureRect( ShadowTexture, ShadowPadding );
		Material.UIBasic.Set( "Color", ShadowTexture );
		Graphics.Quad( shadowRect, Material.UIBasic );
	}

	private static void DrawLoading()
	{
		var loadingRect = GetTextureRect( LoadingTexture );
		Material.UIBasic.Set( "Color", LoadingTexture );
		Graphics.Quad( loadingRect, Material.UIBasic );
	}

	private static void DrawProgress()
	{
		var logoRect = GetTextureRect( LoadingTexture );
		var progressWidth = ProgressBarMaxWidth * Progress;

		var position = new Vector2(
			logoRect.X + ProgressBarHorizontalOffset,
			logoRect.Y + logoRect.Height - ProgressBarVerticalOffset
		);

		Material.UIBasic.Set( "Color", ProgressTexture );
		Graphics.Quad( new Rectangle( position, new Vector2( progressWidth, ProgressBarHeight ) ), Material.UIBasic );
	}

	private static Rectangle GetTextureRect( Texture texture, float padding = 0f )
	{
		var aspect = texture.Width / (float)texture.Height;
		var screenHeight = Screen.Size.Y.Clamp( 0, LoadingTexture.Height );

		var width = screenHeight * aspect + padding;
		var height = screenHeight + padding;

		var position = new Vector2(
			(Screen.Size.X - width) / 2f,
			(Screen.Size.Y - height) / 2f
		);

		return new Rectangle( position, new Vector2( width, height ) );
	}
}
