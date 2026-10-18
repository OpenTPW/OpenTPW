using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace OpenTPW;

public partial class TextureFile
{
	private static byte[] Decompress( byte[] data, TextureFlags codec, int limit )
	{
		if ( data == null || data.Length < 4 )
		{
			throw new InvalidDataException( "Truncated WCT compressed chunk" );
		}
		// Check if the file contains 'BILZ' as the first 4 bytes - if so then it is a zlib
		// compressed chunk
		if ( data[0] == 0x42 && data[1] == 0x49 && data[2] == 0x4C && data[3] == 0x5A )
		{
			return DecompressZLIB( data, limit );
		}

		if ( codec == TextureFlags.Bilz )
		{
			if ( data.Length > limit )
			{
				throw new InvalidDataException( "WCT coefficient data exceeds dimensions" );
			}

			return data;
		}

		return DecompressLZSS( data, limit );
	}

	private static byte[] DecompressZLIB( byte[] data, int limit )
	{
		/*
		 * Theme Park World uses ZLIB 1.1.3
		 * We can therefore deduce that the custom ZLIB header is as follows:
		 * - 4 bytes: "BILZ" magic number
		 * - 4 bytes: decompressed size
		 * - 4 bytes: stream size
		 * - 4 bytes: Constant - MAX_WBITS (15) - from zlib
		 * - 4 bytes: Constant - DEF_MEM_LEVEL (9) - from zlib
		 * - 4 bytes: Unused
		 * - 4 bytes: Unused
		 */
		if ( data.Length < 0x1C )
		{
			throw new InvalidDataException( "Truncated BILZ header" );
		}

		int expectedSize = BitConverter.ToInt32( data, 4 );
		if ( expectedSize < 0 || expectedSize > limit )
		{
			throw new InvalidDataException( "Invalid BILZ output size" );
		}

		data = data[0x1C..];
		using var outputStream = new MemoryStream( expectedSize );
		{
			using var inputStream = new MemoryStream( data );
			using var compressed = new InflaterInputStream( inputStream );
			var buffer = new byte[4096];
			int count;
			while ( (count = compressed.Read( buffer, 0, buffer.Length )) > 0 )
			{
				if ( outputStream.Length + count > expectedSize )
				{
					throw new InvalidDataException( "BILZ output exceeds declared size" );
				}

				outputStream.Write( buffer, 0, count );
			}
		}

		if ( outputStream.Length != expectedSize )
		{
			throw new InvalidDataException( "BILZ output size mismatch" );
		}

		return outputStream.ToArray();
	}

	private static byte[] DecompressLZSS( byte[] data, int limit )
	{
		// 0x5959B6: LSB-first tokens, 12-bit backwards distance, 7-bit length + 1.
		// The shared BitReader returns zero at EOF, causing endless literal output.
		int bit = 0;
		int ReadBits( int count )
		{
			if ( (long)bit + count > (long)data.Length * 8 )
			{
				throw new InvalidDataException( "Truncated WCT LZSS stream" );
			}

			int value = 0;
			for ( int i = 0; i < count; i++, bit++ )
			{
				value |= ((data[bit / 8] >> (bit % 8)) & 1) << i;
			}

			return value;
		}

		var output = new List<byte>();
		while ( true )
		{
			if ( ReadBits( 1 ) == 0 )
			{
				if ( output.Count == limit )
				{
					throw new InvalidDataException( "WCT LZSS output exceeds dimensions" );
				}

				output.Add( (byte)ReadBits( 8 ) );
				continue;
			}

			int distance = ReadBits( 12 );
			if ( distance == 0 )
			{
				return output.ToArray();
			}

			int length = ReadBits( 7 ) + 1;
			if ( distance > output.Count || output.Count + length > limit )
			{
				throw new InvalidDataException( "Invalid WCT LZSS match" );
			}

			for ( int i = 0; i < length; i++ )
			{
				output.Add( output[output.Count - distance] );
			}
		}
	}
}
