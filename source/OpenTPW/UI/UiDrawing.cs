namespace OpenTPW.UI;

internal static class UiDrawing
{
	public static void Sprite( Texture texture, Rectangle bounds ) => Sprite( texture, bounds, new Rectangle( 0, 0, 1, 1 ) );

	public static void Sprite( Texture texture, Rectangle bounds, Rectangle uv )
	{
		using var context = new ResourceContext();
		context.Set( "Color", texture );
		Graphics.Quad( bounds, uv, Material.UIBasic );
	}

	public static void Sprite( string name, Rectangle bounds, TextureFlags flags = TextureFlags.None ) =>
		Sprite( UiAssets.GetTexture( name, flags ), bounds );

	// Rectangle bounds are for models normalized to Vector2.One by UiModel.
	public static void Model( Model model, Rectangle bounds ) =>
		Model( model, new Vector2( bounds.X, bounds.Y ), new Vector2( bounds.Width, bounds.Height ) );

	public static void Model( Model model, Vector2 position, Vector2 scale )
	{
		using var scope = new Graphics.Scope( (Point2)Screen.RawSize, Graphics.CreateScreenMatrix( (Point2)Screen.RawSize ) );
		model.Draw( new Transform
		{
			Position = new Vector3( position.X, position.Y, 0 ),
			Scale = new Vector3( scale.X, scale.Y, 1 )
		} );
	}
}
