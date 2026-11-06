using System.Numerics;

namespace OpenTPW.UI;

public class Panel
{
	public Vector2 position;
	public Vector2 size;

	public Matrix4x4 modelMatrix;

	public Panel()
	{
		position = new();
		size = new( 1.0f, 1.0f );
	}

	private Vector2 SizeToNDC( Vector2 size )
	{
		var p = size / new Vector2( Screen.Size.X, Screen.Size.Y );
		return p;
	}

	private Vector2 PositionToNDC( Vector2 position )
	{
		var p = position / new Vector2( Screen.Size.X, Screen.Size.Y );
		p += new Vector2( -0.5f, -0.5f );
		p *= 2.0f;
		return p;
	}

	public void Update()
	{
		OnUpdate();
	}

	protected virtual void OnRender() { }
	protected virtual void OnUpdate() { }
	protected virtual void OnDelete() { }

	public void Draw()
	{
		var positionNDC = PositionToNDC( position );
		var sizeNDC = SizeToNDC( size );

		modelMatrix = Matrix4x4.CreateScale( sizeNDC.X, sizeNDC.Y, 1 ) *
			Matrix4x4.CreateTranslation( positionNDC.X, positionNDC.Y, 1 );

		OnRender();
	}

	public void Delete()
	{
		OnDelete();
		Level.Current.Hud.Children.Remove( this );
	}
}
