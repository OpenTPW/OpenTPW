
namespace OpenTPW;

public struct Point2
{
	public int X { get; set; }

	public int Y { get; set; }

	public static readonly Point2 One = new( 1 );

	public static readonly Point2 Zero = new( 0 );

	public static readonly Point2 OneX = new( 1, 0 );

	public static readonly Point2 OneY = new( 0, 1 );

	public Point2( int x, int y )
	{
		X = x;
		Y = y;
	}

	public Point2( Point2 other ) : this( other.X, other.Y )
	{

	}

	public Point2( int all = 0 ) : this( all, all )
	{

	}

	public static explicit operator Point2( Vector2 v ) => new Point2( (int)v.X, (int)v.Y );
}
