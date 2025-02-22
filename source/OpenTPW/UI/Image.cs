namespace OpenTPW.UI;

public partial class Image : Panel
{
	private Texture Texture;

	public Vector2 Uv0 { get; set; } = new Vector2( 0, 0 );
	public Vector2 Uv1 { get; set; } = new Vector2( 1, 1 );

	public Vector2 Position { get; set; }
	public Vector2 Size { get; set; }

	public Rectangle Rect => new Rectangle( Position, Size );
	public Rectangle UvRect => new Rectangle( Uv0, Uv1 - Uv0 );

	public Image( string texturePath )
	{
		Texture = new Texture( texturePath );
	}

	public Image( Texture texture )
	{
		Texture = texture;
	}

	protected override void OnRender()
	{
		var material = Material.UIBasic;
		material.Set( "Color", Texture );
		Graphics.Quad( Rect, UvRect, material );
	}
}
