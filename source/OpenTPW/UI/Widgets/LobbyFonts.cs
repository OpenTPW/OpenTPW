namespace OpenTPW;

/// <summary>640x480 font set 1: Font_GetSlot (US 0x485a70) indexes 24-byte records at Font_SlotTable (US 0x78b54c).</summary>
internal static class LobbyFonts
{
	// Text_LoadStartupResources (US 0x419710) loads each record's font at +0x14, starting at Font_SlotTable (US 0x78b54c), position pending.
	private static readonly string[] Names = [ "MENUMED", "CASHMED", "SESHMED", "DATESMALL", "GAME10AA",
		"TITLEMED", "GAME7", "GAME8", "GAME8AA", "GAME7", "CONSOLE6", "GAME7", "POSTCARD" ];
	public static string Name( int slot ) => slot is >= 0 and < 13 ? Names[slot] : throw new ArgumentOutOfRangeException( nameof(slot) );
}
