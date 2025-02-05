namespace OpenTPW;

public struct Vector2 : IEquatable<Vector2>
{
	private System.Numerics.Vector2 internalVector;

	public float X
	{
		readonly get => internalVector.X;
		set => internalVector.X = value;
	}

	public float Y
	{
		readonly get => internalVector.Y;
		set => internalVector.Y = value;
	}

	public static readonly Vector2 One = new( 1f );

	public static readonly Vector2 Zero = new( 0f );

	public static readonly Vector2 OneX = new( 1f, 0f );

	public static readonly Vector2 OneY = new( 0f, 1f );

	public readonly float Length => internalVector.Length();
	public readonly float LengthSquared => internalVector.LengthSquared();

	public readonly Vector2 Normal => this / Length;
	public void Normalize()
	{
		internalVector = new System.Numerics.Vector2( X, Y ) / Length;
	}

	public Vector2( float x, float y )
	{
		internalVector = new System.Numerics.Vector2( x, y );
	}

	public Vector2( Vector2 other ) : this( other.X, other.Y )
	{

	}

	public Vector2( float all = 0.0f ) : this( all, all )
	{

	}

	internal Vector2( System.Numerics.Vector2 other ) : this( other.X, other.Y )
	{

	}

	public static implicit operator Vector2( System.Numerics.Vector2 value ) => new Vector2( value.X, value.Y );

	public static implicit operator System.Numerics.Vector2( Vector2 value ) => new System.Numerics.Vector2( value.X, value.Y );

	public static Vector2 operator +( Vector2 a, Vector2 b ) => new Vector2( a.X + b.X, a.Y + b.Y );

	public static Vector2 operator +( Vector2 a, float b ) => new Vector2( a.X + b, a.Y + b );

	public static Vector2 operator -( Vector2 a, Vector2 b ) => new Vector2( a.X - b.X, a.Y - b.Y );

	public static Vector2 operator -( Vector2 a, float b ) => new Vector2( a.X - b, a.Y - b );

	public static Vector2 operator *( Vector2 a, float f ) => new Vector2( a.X * f, a.Y * f );

	public static Vector2 operator *( Vector2 a, Vector2 b ) => new Vector2( a.X * b.X, a.Y * b.Y );

	public static Vector2 operator *( float f, Vector2 a ) => new Vector2( a.X * f, a.Y * f );

	public static Vector2 operator *( Vector2 a, System.Numerics.Matrix4x4 transform ) => System.Numerics.Vector2.Transform( a, transform );

	public static Vector2 operator /( Vector2 a, float f ) => new Vector2( a.X / f, a.Y / f );
	public static Vector2 operator /( Vector2 a, Vector2 b ) => new Vector2( a.X / b.X, a.Y / b.Y );

	public static Vector2 operator -( Vector2 value ) => new Vector2( 0f - value.X, 0f - value.Y );

	public static bool operator ==( Vector2 left, Vector2 right ) => left.Equals( right );

	public static bool operator !=( Vector2 left, Vector2 right ) => !(left == right);


	public static explicit operator Vector2( Point2 v ) => new Vector2( v.X, v.Y );

	public override bool Equals( object? obj )
	{
		if ( obj is Vector2 vec )
		{
			return vec.X == X && vec.Y == Y;
		}

		return false;
	}

	public bool Equals( Vector2 other ) => Equals( other );

	public static float Dot( Vector2 a, Vector2 b ) => System.Numerics.Vector2.Dot( a.internalVector, b.internalVector );

	public readonly float Dot( Vector2 b ) => Dot( this, b );

	public readonly float Distance( Vector2 target ) => DistanceBetween( this, target );

	public static float DistanceBetween( Vector2 a, Vector2 b ) => (b - a).Length;

	public static Vector2 Reflect( Vector2 direction, Vector2 normal ) => direction - 2f * Dot( direction, normal ) * normal;

	public readonly Vector2 WithX( float x_ ) => new Vector2( x_, Y );
	public readonly Vector2 WithY( float y_ ) => new Vector2( X, y_ );

	public override int GetHashCode() => HashCode.Combine( internalVector );

	public override string ToString() => internalVector.ToString();
}
