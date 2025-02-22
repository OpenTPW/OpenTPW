namespace OpenTPW.UI;

public class Cursor : Panel, Singleton<Cursor>
{
	private Texture? _texture;
	private Material _material;

	public Cursor()
	{
		_material = Material.UIBasic.Copy();

		LoadCursorTexture();
	}

	private string GetImageName( Input.CursorTypes cursorType )
	{
		if ( cursorType == Input.CursorTypes.Cash )
			return "Cash"; // This one isn't technically prefixed with a C

		return "C" + cursorType.ToString()[..3].ToLower();
	}

	private void LoadCursorTexture()
	{
		var cursorPath = $"/data/ui/cursors/{GetImageName( Input.CursorType )}.tga";
		_texture = new Texture( GameDir.GetPath( cursorPath ), TextureFlags.PinkChromaKey );

		_material.Set( "Color", _texture );
	}

	protected override void OnUpdate()
	{
		Bounds = new Rectangle(
		  	Input.Mouse.Position,
			new Vector2( 32f )
		);
	}

	protected override void OnRender()
	{
		int numFrames = 4;
		int speed = 3;

		var time = Time.Now * speed;
		var currentFrame = time.CeilToInt() % numFrames;

		var uv0 = new Vector2( currentFrame * 0.25f, 0f );
		var uv1 = uv0 + new Vector2( 0.25f, 1f );

		var uvRect = new Rectangle( uv0, uv1 - uv0 );

		using ( _ = new Graphics.Scope( (Point2)Screen.Size ) )
		{
			Graphics.Quad( Bounds, uvRect, _material );
		}
	}
}
