using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>Layout metrics used by tp_nocd.exe's BF4 reader (006B0580/006B05E0).</summary>
public sealed class Bf4Metrics
{
	// Offsets in the original BF4 header and 24-byte glyph records.
	private const int HeaderBytes = 8;
	private const int LineHeightOffset = 5;
	private const int GlyphCountOffset = 6;
	private const int GlyphRecordBytes = 24;
	private const int GlyphWidthOffset = 16;
	private const int GlyphHeightOffset = 18;
	private const int BearingXOffset = 20;
	private const int BearingYOffset = 21;
	private const int AdvanceOffset = 22;
	public readonly record struct Glyph( uint Codepoint, ushort Width, ushort Height, sbyte OffsetX, sbyte OffsetY, ushort Advance );

	public byte LineHeight { get; }
	public IReadOnlyDictionary<uint, Glyph> Glyphs { get; }

	public Bf4Metrics( ReadOnlySpan<byte> bytes )
	{
		if ( bytes.Length < HeaderBytes || !bytes[..4].SequenceEqual( "F4FB"u8 ) )
		{
			throw new InvalidDataException( "Invalid BF4 header." );
		}

		LineHeight = bytes[LineHeightOffset];
		int count = BinaryPrimitives.ReadUInt16LittleEndian( bytes[GlyphCountOffset..] );
		int tableEnd = HeaderBytes + count * sizeof( uint );
		if ( LineHeight == 0 || count == 0 || tableEnd > bytes.Length )
		{
			throw new InvalidDataException( "Invalid BF4 glyph table." );
		}

		var glyphs = new Dictionary<uint, Glyph>();
		for ( int i = 0; i < count; ++i )
		{
			uint offset = BinaryPrimitives.ReadUInt32LittleEndian( bytes[(HeaderBytes + i * sizeof( uint ))..] );
			if ( offset < tableEnd || offset > bytes.Length - GlyphRecordBytes )
			{
				throw new InvalidDataException( "BF4 glyph offset is out of bounds." );
			}

			var record = bytes[(int)offset..];
			uint code = BinaryPrimitives.ReadUInt32LittleEndian( record );
			glyphs[code] = new( code,
				BinaryPrimitives.ReadUInt16LittleEndian( record[GlyphWidthOffset..] ),
				BinaryPrimitives.ReadUInt16LittleEndian( record[GlyphHeightOffset..] ),
				(sbyte)record[BearingXOffset],
				(sbyte)record[BearingYOffset],
				BinaryPrimitives.ReadUInt16LittleEndian( record[AdvanceOffset..] ) );
		}

		Glyphs = glyphs;
	}
}
