namespace OpenTPW;

/// <summary>The CRT stream used by retail rain initialization and idle animations (0x675a40).</summary>
internal static class RetailRandom
{
	private static uint state = 1;
	internal static int Next() => Advance( ref state );
	internal static int Advance( ref uint value )
	{
		value = unchecked( value * 0x343fd + 0x269ec3 );
		return (int)((value >> 16) & 0x7fff);
	}
}
