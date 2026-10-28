using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace OpenTPW;

public record struct TextureData( int Width, int Height, byte[] Data );

public partial class TextureFile : BaseFormat
{
	public TextureData Data { get; set; }

	static float ComputeDequantizationScaleY( int n ) => 1.0f - ((float)n * -0.5f);
	static float ComputeDequantizationScaleCbCr( int n ) => 1.0f - ((float)n * -0.25f);
	static float ComputeDequantizationScaleA( int n ) => (float)n + 1.0f;

	public TextureFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	public TextureFile( string path )
	{
		ReadFromFile( path );
	}

	protected override void ReadFromStream( Stream stream )
	{
		if ( stream == null )
		{
			Data = new TextureData( 1, 1, new byte[] { 255, 0, 255, 255 } );
			return;
		}

		using var binaryReader = new BinaryReader( stream );

		//
		// Read header
		//
		var fileData = new TextureFileData()
		{
			Type = (WctType)binaryReader.ReadByte(),
			HasAlphaChannel = binaryReader.ReadByte() == 0x01,
			BitsPerPixel = binaryReader.ReadByte(),
			Version = binaryReader.ReadByte(),
			Width = binaryReader.ReadInt16(),
			Height = binaryReader.ReadInt16(),
			YChannelQuantizationScale = binaryReader.ReadInt16(),
			CbChannelQuantizationScale = binaryReader.ReadInt16(),
			CrChannelQuantizationScale = binaryReader.ReadInt16(),
			AChannelQuantizationScale = binaryReader.ReadInt16(),

			ColorChunkSize = binaryReader.ReadInt32(),
			AlphaChunkSize = binaryReader.ReadInt32(),

			Checksum = binaryReader.ReadInt32()
		};
		if ( fileData.Type is not (WctType.ZlibPacked or WctType.LzssInterleaved) )
			throw new InvalidDataException( $"Unsupported WCT type: {(byte)fileData.Type:X2}" );

		//
		// Read blocks
		//

		// Color
		if ( fileData.ColorChunkSize == 0 )
			throw new InvalidDataException( "Texture does not have color data" );

		fileData.ColorChunk = binaryReader.ReadBytes( fileData.ColorChunkSize );

		// Alpha
		if ( fileData.AlphaChunkSize > 0 )
			fileData.AlphaChunk = binaryReader.ReadBytes( fileData.AlphaChunkSize );

		//
		// Decompress blocks
		//
		var alphaData = Array.Empty<byte>();
		var colorData = Decompress( fileData.ColorChunk );

		if ( fileData.HasAlphaChannel )
			alphaData = Decompress( fileData.AlphaChunk );

		//
		// Decode image
		//
		var size = (int)Math.Max( GetAlignedSize( (uint)Math.Max( fileData.Height, fileData.Width ) ), 8 );

		var outputY = Enumerable.Repeat( 0f, size * size ).ToList();
		var outputCb = Enumerable.Repeat( 0f, size * size ).ToList();
		var outputCr = Enumerable.Repeat( 0f, size * size ).ToList();
		var outputA = Enumerable.Repeat( 255f, size * size ).ToList();

		var state = new ImageDecodeState( size );
		// 0x58e510 dispatches 0x12 to 0x58ff60 (packed row coefficients),
		// and 0x13 to 0x591dd0 (interleaved). Both reconstruct the full native size.
		bool packed = fileData.Type == WctType.ZlibPacked;
		DecodeChannel( ref state, size, ref colorData, ref outputY, ComputeDequantizationScaleY( fileData.YChannelQuantizationScale ), packed );
		DecodeChannel( ref state, size / 2, ref colorData, ref outputCb, ComputeDequantizationScaleCbCr( fileData.CbChannelQuantizationScale ), packed );
		DecodeChannel( ref state, size / 2, ref colorData, ref outputCr, ComputeDequantizationScaleCbCr( fileData.CrChannelQuantizationScale ), packed );

		if ( fileData.HasAlphaChannel )
			DecodeChannel( ref state, size, ref alphaData, ref outputA, ComputeDequantizationScaleA( fileData.AChannelQuantizationScale ), packed );

		uint alignedWidth = GetAlignedSize( (uint)fileData.Width );
		uint alignedHeight = GetAlignedSize( (uint)fileData.Height );

		var outputBytes = Enumerable.Repeat( 0f, fileData.Width * fileData.Height * 4 ).ToList();

		for ( int y = 0; y < fileData.Height; y++ )
		{
			for ( int x = 0; x < fileData.Width; x++ )
			{
				// Convert from ycbcr to rgb
				float cy = outputY[y * size + x];
				float cb = outputCb[((y / 2) * (size / 2) + (x / 2))];
				float cr = outputCr[((y / 2) * (size / 2) + (x / 2))];

				float r = cy + 1.402f * (cr);
				float g = cy - 0.344136f * (cb) - 0.714136f * (cr);
				float b = cy + 1.772f * (cb);

				// Alpha channel remains untouched..
				float a = outputA[y * size + x];

				outputBytes[((y * fileData.Width + x) * 4)] = r;
				outputBytes[((y * fileData.Width + x) * 4) + 1] = g;
				outputBytes[((y * fileData.Width + x) * 4) + 2] = b;
				outputBytes[((y * fileData.Width + x) * 4) + 3] = a;
			}
		}

		List<byte> textureData = Enumerable.Repeat( (byte)0, fileData.Width * fileData.Height * 4 ).ToList();
		for ( int i = 0; i < outputBytes.Count; i++ )
		{
			textureData[i] = (byte)outputBytes[i].Clamp( 0, 255 );
		}

		Data = new TextureData( fileData.Width, fileData.Height, textureData.ToArray() );
	}

	private static byte[] Decompress( byte[] data )
	{
		// Check if the file contains 'BILZ' as the first 4 bytes - if so then it is a zlib
		// compressed chunk
		if ( data[0] == 0x42 && data[1] == 0x49 && data[2] == 0x4C && data[3] == 0x5A )
			return DecompressZLIB( data );

		// Not zlib, so must be data compressed with LZSS
		return DecompressLZSS( data );
	}

	private static byte[] DecompressZLIB( byte[] data )
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

		// We don't need to use any of this data - so we'll just skip it entirely
		data = data[0x1C..]; // Unused data

		const int maxSize = 256 * 256 * 3 + 128 * 128 * 3 + 128 * 128 * 3 + 256 * 256 * 3;
		using var outputStream = new MemoryStream( maxSize );
		using var outputWriter = new BinaryWriter( outputStream );
		{
			using var inputStream = new MemoryStream( data );
			using var compressed = new InflaterInputStream( inputStream );
			compressed.CopyTo( outputStream );
		}

		return outputStream.ToArray();
	}

	private static byte[] DecompressLZSS( byte[] data )
	{
		/*					Y (full)      + Cb (half)     + Cr (half)     + Alpha (full) */
		const int maxSize = 256 * 256 * 3 + 128 * 128 * 3 + 128 * 128 * 3 + 256 * 256 * 3;
		using var outputStream = new MemoryStream( maxSize );
		using var outputWriter = new BinaryWriter( outputStream );

		var blockReader = new BitReader( data );
		if ( !LZSS.Decompress( blockReader, outputWriter ) )
		{
			throw new Exception( "LZSS failed to decompress" );
		}

		return outputStream.ToArray();
	}
}
