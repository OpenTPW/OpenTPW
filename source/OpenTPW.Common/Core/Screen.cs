namespace OpenTPW;

public static class Screen
{
	public static Vector2 Size => RawSize / DpiScale;
	public static Vector2 RawSize { get; set; } = new( 1, 1 );

	public static float Aspect => (float)Size.X / (float)Size.Y;
	public static float DpiScale { get; set; } = 1.0f;

	public static void UpdateFrom( Point2 size )
	{
		RawSize = (Vector2)size;
	}
}
