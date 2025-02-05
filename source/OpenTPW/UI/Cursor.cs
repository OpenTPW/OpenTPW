namespace OpenTPW.UI;

public class Cursor : Panel, Singleton<Cursor>
{
	private Texture? _texture;

	private Input.CursorTypes _cursorType;

	public Input.CursorTypes CursorType
	{
		get => _cursorType;
		set
		{
			_cursorType = value;
			LoadTexture();
		}
	}

	public Cursor()
	{
		// TODO: Move this to Input
		CursorType = Input.CursorTypes.Normal;
	}

	private string GetImageName( Input.CursorTypes cursorType )
	{
		// bullfrog PLEASE...
		if ( cursorType == Input.CursorTypes.Cash )
			return "Cash";

		return "C" + cursorType.ToString()[..3].ToLower();
	}

	private void LoadTexture()
	{
		var cursorPath = $"/data/ui/cursors/{GetImageName( _cursorType )}.tga";
		_texture = new Texture( GameDir.GetPath( cursorPath ), TextureFlags.PinkChromaKey );
	}

	protected override void OnRender()
	{
		if ( _texture == null )
			return;

		var material = Material.UI;
		material.Set( "Color", _texture );

		var size = new Vector2( 32 );

		var position = Input.Mouse.Position;
		position.Y = Screen.Size.Y - position.Y;
		position.Y -= size.Y;

		var frame = ((Time.Now * 3).CeilToInt()) % 4;
		var uv0 = new Vector2( frame * 0.25f, 0f );
		var uv1 = uv0 + new Vector2( 0.25f, 1f );

		var rect = new Rectangle( position, size );
		var uvRect = new Rectangle( uv0, uv1 - uv0 );

		using ( _ = new Graphics.Scope( (Point2)Screen.Size ) )
		{
			Graphics.Quad( rect, uvRect, material );
		}
	}
}
