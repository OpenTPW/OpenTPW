namespace OpenTPW.UI;

/// <summary>
/// Host-selected BF4 font slots: Font_GetSlot (US 0x485a70; TPI 0x500de0).
/// The host selects the resolution bank and supplies language-relative resource streams.
/// </summary>
public sealed class Bf4FontSet
{
	private readonly FontFile[] fonts;
	public int Count => fonts.Length;
	public FontFile this[int slot] => fonts[slot];

	public Bf4FontSet( ResourceIndexFile index, IEnumerable<uint> slots, Func<string, Stream> open )
	{
		var loaded = new Dictionary<uint, FontFile>();
		fonts = slots.Select( id =>
		{
			if ( loaded.TryGetValue( id, out var font ) ) return font;
			using var stream = open( index[id].Name );
			font = new FontFile( stream );
			loaded.Add( id, font );
			return font;
		} ).ToArray();
		if ( fonts.Length == 0 ) throw new ArgumentException( "A font set must contain slots.", nameof( slots ) );
	}

	public string[] Layout( int slot, string text, float width ) =>
		Bf4TextLayout.Layout( text, width, part => fonts[slot].MeasureString( part ) );
}
