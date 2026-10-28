namespace OpenTPW;

/// <summary>Paletted TPC frames loaded by TP.exe 0x584320/0x584bd0.</summary>
public sealed class SpriteTextureFile
{
	public sealed record Frame( int Width, int Height, int DisplayWidth, int DisplayHeight,
		int OffsetX, int OffsetY, byte[] Indices );
	public ushort Variant { get; }
	// Preserve native packed palette values until the rendering format is selected.
	public uint[] Palette { get; }
	public IReadOnlyList<Frame> Frames { get; }

	public SpriteTextureFile( byte[] data )
	{
		using var stream = new MemoryStream( data, false );
		using var reader = new BinaryReader( stream );
		try
		{
			if ( reader.ReadUInt16() != 3 ) throw new InvalidDataException( "Unsupported TPC pixel format" );
			Variant = reader.ReadUInt16();
			int count = reader.ReadInt32();
			if ( count < 1 || count > 4096 ) throw new InvalidDataException( "Invalid TPC frame count" );
			Palette = new uint[256];
			for ( int i = 0; i < Palette.Length; i++ ) Palette[i] = reader.ReadUInt32();
			var frames = new List<Frame>( count );
			long pixels = 0;
			for ( int i = 0; i < count; i++ )
			{
				int size = reader.ReadInt32();
				int width = reader.ReadUInt16(), height = reader.ReadUInt16();
				int displayWidth = reader.ReadUInt16(), displayHeight = reader.ReadUInt16();
				int offsetX = reader.ReadInt32(), offsetY = reader.ReadInt32();
				if ( width < 1 || height < 1 || width > 4096 || height > 4096 || displayWidth < 1 || displayHeight < 1 ||
					size < 1 || size > stream.Length - stream.Position || (pixels += (long)width * height) > 64 * 1024 * 1024 )
					throw new InvalidDataException( $"Invalid TPC frame {i}" );
				var encoded = reader.ReadBytes( size );
				frames.Add( new Frame( width, height, displayWidth, displayHeight, offsetX, offsetY, Decode( encoded, width, height ) ) );
			}
			if ( stream.Position != stream.Length ) throw new InvalidDataException( "Trailing TPC data" );
			Frames = frames;
		}
		catch ( EndOfStreamException exception )
		{
			throw new InvalidDataException( "Truncated TPC data", exception );
		}
	}

	private static byte[] Decode( byte[] data, int width, int height )
	{
		// 0x560db0: each row has a byte length, then signed literal/repeat packets.
		var pixels = new byte[checked(width * height)];
		int source = 0;
		for ( int row = 0; row < height; row++ )
		{
			if ( source >= data.Length ) throw new InvalidDataException( "Missing TPC row" );
			int end = source + 1 + data[source];
			source++;
			if ( end > data.Length ) throw new InvalidDataException( "Truncated TPC row" );
			int x = 0;
			while ( source < end )
			{
				int packet = unchecked((sbyte)data[source++]);
				int count = Math.Abs( packet );
				if ( count > width - x ) throw new InvalidDataException( "TPC packet exceeds row width" );
				if ( packet > 0 )
				{
					if ( count > end - source ) throw new InvalidDataException( "Truncated TPC literal packet" );
					Array.Copy( data, source, pixels, row * width + x, count );
					source += count;
				}
				else
				{
					if ( source >= end ) throw new InvalidDataException( "Truncated TPC repeat packet" );
					Array.Fill( pixels, data[source++], row * width + x, count );
				}
				x += count;
			}
			if ( x != width ) throw new InvalidDataException( "Incomplete TPC row" );
		}
		if ( source != data.Length ) throw new InvalidDataException( "Trailing TPC frame data" );
		return pixels;
	}
}
