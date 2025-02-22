namespace OpenTPW;

public record struct Rectangle( float X, float Y, float Width, float Height )
{
	public readonly Vector2 TopLeft => new Vector2( X, Y + Height );
	public readonly Vector2 TopRight => new Vector2( X + Width, Y + Height );
	public readonly Vector2 BottomLeft => new Vector2( X, Y );
	public readonly Vector2 BottomRight => new Vector2( X + Width, Y );

	public Rectangle( Vector2 position, Vector2 size ) : this( position.X, position.Y, size.X, size.Y )
	{

	}

	public Rectangle Shift( Vector2 offset )
	{
		return new Rectangle( X + offset.X, Y + offset.Y, Width, Height );
	}

	public bool Contains( Vector2 position )
	{
		return position.X >= TopLeft.X &&
			   position.Y <= TopLeft.Y &&
			   position.X <= BottomRight.X &&
			   position.Y >= BottomRight.Y;
	}
}
