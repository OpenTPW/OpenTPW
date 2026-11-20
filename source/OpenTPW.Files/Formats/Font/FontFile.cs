using System.Text;

namespace OpenTPW;

/// <summary>
/// A bitmap font ("Language/&lt;language&gt;/*.bf4"). It has one glyph per character of the
/// language's code page (MBToUni.dat), in the same order.
/// </summary>
public sealed class FontFile : BaseFormat
{
	/*
	Header
		4 bytes: Magic number - "F4FB"
		1 byte:  Version (1 or 2)
		1 byte:  Size hint
		2 bytes: Glyph count
		4 bytes per glyph: Glyph offset

	Glyph
		4 bytes: Character (UTF-16)
		4 bytes: Bitmap size in bytes (a multiple of 8)
		4 bytes: Size of the bitmap at 4 bits per pixel
		4 bytes: Encoding - 0: 4 bits per pixel, high nibble first; 1: run-length coded 4 bits per
		         pixel (see DecodeRuns); 2: 1 bit per pixel
		2 bytes: Width
		2 bytes: Height
		1 byte:  X offset (signed)
		1 byte:  Y offset from the top of the line (signed; a few glyphs such as '$' start above it)
		2 bytes: Advance
		Bitmap:  rows not padded; at 1 bit per pixel, most significant bit first
	*/

	public class Glyph
	{
		public char Character { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public int OffsetX { get; set; }
		public int OffsetY { get; set; }
		public int Advance { get; set; }

		/// <summary>
		/// 0: 4 bits per pixel; 1: run-length coded 4 bits per pixel; 2: 1 bit per pixel.
		/// </summary>
		public int Encoding { get; set; }

		/// <summary>
		/// Coverage from 0 to 255, Width * Height, row by row.
		/// </summary>
		public byte[] Pixels { get; set; } = [];
	}

	public int Version { get; private set; }
	public int SizeHint { get; private set; }
	public Glyph[] Glyphs { get; private set; } = [];

	/// <summary>
	/// Height of a line: the lowest glyph bottom.
	/// </summary>
	public int LineHeight { get; private set; }

	/// <summary>
	/// The highest glyph top; negative when some glyphs start above the line.
	/// </summary>
	public int Top { get; private set; }

	private readonly Dictionary<char, Glyph> glyphsByCharacter = new();

	public FontFile( string path )
	{
		ReadFromFile( path );
	}

	public FontFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	protected override void ReadFromStream( Stream stream )
	{
		using var reader = new BinaryReader( stream, Encoding.ASCII, true );

		var magicNumber = Encoding.ASCII.GetString( reader.ReadBytes( 4 ) );
		if ( magicNumber != "F4FB" )
			throw new Exception( $"Magic number did not match: {magicNumber}" );

		Version = reader.ReadByte();
		SizeHint = reader.ReadByte();
		var glyphCount = reader.ReadUInt16();

		var offsets = new uint[glyphCount];
		for ( int i = 0; i < glyphCount; i++ )
			offsets[i] = reader.ReadUInt32();

		Glyphs = new Glyph[glyphCount];
		for ( int i = 0; i < glyphCount; i++ )
		{
			reader.BaseStream.Seek( offsets[i], SeekOrigin.Begin );

			var glyph = new Glyph { Character = (char)reader.ReadUInt32() };
			var bitmapSize = (int)reader.ReadUInt32();
			_ = reader.ReadUInt32();
			glyph.Encoding = (int)reader.ReadUInt32();
			glyph.Width = reader.ReadUInt16();
			glyph.Height = reader.ReadUInt16();
			glyph.OffsetX = reader.ReadSByte();
			glyph.OffsetY = reader.ReadSByte();
			glyph.Advance = reader.ReadUInt16();

			var bitmap = reader.ReadBytes( bitmapSize );
			glyph.Pixels = new byte[glyph.Width * glyph.Height];
			if ( glyph.Encoding == 1 )
				DecodeRuns( bitmap, glyph.Pixels );

			for ( int p = 0; p < glyph.Pixels.Length; p++ )
			{
				if ( glyph.Encoding == 2 && (p >> 3) < bitmap.Length )
					glyph.Pixels[p] = (bitmap[p >> 3] & (0x80 >> (p & 7))) != 0 ? (byte)255 : (byte)0;
				else if ( glyph.Encoding == 0 && (p >> 1) < bitmap.Length )
					glyph.Pixels[p] = (byte)(((bitmap[p >> 1] >> ((p & 1) == 0 ? 4 : 0)) & 0xF) * 17);
			}

			Glyphs[i] = glyph;
			if ( glyph.Width * glyph.Height > 0 )
			{
				LineHeight = Math.Max( LineHeight, glyph.OffsetY + glyph.Height );
				Top = Math.Min( Top, glyph.OffsetY );
			}

			// Unused slots repeat a placeholder character; keep the first
			glyphsByCharacter.TryAdd( glyph.Character, glyph );
		}
	}

	/// <summary>
	/// Encoding 1, read as 4-bit units, high nibble first: a non-zero unit is one pixel of that value;
	/// a zero is followed by a count and a value, for that many pixels. Runs may end past the last
	/// pixel, and pixels after the data ends are transparent.
	/// </summary>
	private static void DecodeRuns( byte[] data, byte[] pixels )
	{
		int Nibble( int n ) => (data[n >> 1] >> ((n & 1) == 0 ? 4 : 0)) & 0xF;

		int count = data.Length * 2, n = 0, p = 0;
		while ( p < pixels.Length && n < count )
		{
			int value = Nibble( n++ ), run = 1;
			if ( value == 0 )
			{
				if ( n + 1 >= count )
					break;

				run = Nibble( n++ );
				value = Nibble( n++ );
			}

			for ( ; run > 0 && p < pixels.Length; run-- )
				pixels[p++] = (byte)(value * 17);
		}
	}

	public Glyph? GetGlyph( char character ) => glyphsByCharacter.GetValueOrDefault( character );

	public int MeasureString( string text ) => text.Sum( c => GetGlyph( c )?.Advance ?? 0 );

	/// <summary>
	/// Draws a line of text as RGBA pixels in <paramref name="color"/> on a transparent background.
	/// </summary>
	public TextureData RenderString( string text, uint color = 0xFFFFFFFF )
	{
		int width = Math.Max( 1, MeasureString( text ) );
		int height = Math.Max( 1, LineHeight - Top );
		var data = new byte[width * height * 4];

		int penX = 0;
		foreach ( var c in text )
		{
			var glyph = GetGlyph( c );
			if ( glyph == null )
				continue;

			for ( int y = 0; y < glyph.Height; y++ )
			{
				for ( int x = 0; x < glyph.Width; x++ )
				{
					int px = penX + glyph.OffsetX + x, py = glyph.OffsetY - Top + y;
					var coverage = glyph.Pixels[y * glyph.Width + x];
					if ( coverage == 0 || px < 0 || px >= width || py < 0 || py >= height )
						continue;

					int o = (py * width + px) * 4;
					data[o] = (byte)(color >> 24);
					data[o + 1] = (byte)(color >> 16);
					data[o + 2] = (byte)(color >> 8);
					data[o + 3] = (byte)((color & 0xFF) * coverage / 255);
				}
			}

			penX += glyph.Advance;
		}

		return new TextureData( width, height, data );
	}
}
