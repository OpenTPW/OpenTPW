namespace OpenTPW;

/// <summary>640x480 font set 1: 0x483d20 indexes 24-byte records at 0x786e04.</summary>
internal static class LobbyFonts
{
	// 0x419390 loads each record's font at +0x14, starting at 0x786e18.
	private static readonly string[] Names = [ "MENUMED", "CASHMED", "SESHMED", "DATESMALL", "GAME10AA",
		"TITLEMED", "GAME7", "GAME8", "GAME8AA", "GAME7", "CONSOLE6", "GAME7", "POSTCARD" ];
	public static string Name( int slot ) => slot is >= 0 and < 13 ? Names[slot] : throw new ArgumentOutOfRangeException( nameof(slot) );
}
