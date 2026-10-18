namespace OpenTPW;

public record struct TextureData( int Width, int Height, byte[] Data );

public partial class TextureFile : BaseFormat
{
	// Reproduce the original x87/MMX rounding rather than modern color conversion.
	private const double RoundingBias = 12582912.0;

	public TextureData Data { get; set; }

	// SGN stores the same wavelet chunks as WCT, with floating point scales.
	internal static TextureData DecodeSignBackground( int width, int height, float y, float cb, float cr, float a,
		byte[] color, byte[] alpha )
	{
		ushort Quant( float scale, float step )
		{
			float value = (scale - 1) / step;
			if ( !float.IsFinite( value ) || value < 0 || value > ushort.MaxValue || value != MathF.Truncate( value ) )
				throw new InvalidDataException( "Unsupported SGN quantization scale." );
			return (ushort)value;
		}
		using var stream = new MemoryStream();
		using ( var writer = new BinaryWriter( stream, System.Text.Encoding.UTF8, true ) )
		{
			writer.Write( new byte[] { 0x12, 1, 32, 3 } );
			writer.Write( (ushort)width ); writer.Write( (ushort)height );
			writer.Write( Quant( y, .5f ) ); writer.Write( Quant( cb, .25f ) );
			writer.Write( Quant( cr, .25f ) ); writer.Write( Quant( a, 1 ) );
			writer.Write( color.Length ); writer.Write( alpha.Length ); writer.Write( 0 );
			writer.Write( color ); writer.Write( alpha );
		}
		stream.Position = 0;
		return new TextureFile( stream ).Data;
	}

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
		if ( stream.CanSeek && stream.Length - stream.Position < 28 )
		{
			throw new InvalidDataException( "Truncated WCT header" );
		}

		var fileData = ReadHeader( binaryReader );
		if ( stream.CanSeek && (long)fileData.ColorChunkSize + fileData.AlphaChunkSize > stream.Length - stream.Position )
		{
			throw new InvalidDataException( "Truncated WCT chunks" );
		}

		fileData.ColorChunk = ReadChunk( binaryReader, fileData.ColorChunkSize );
		if ( fileData.AlphaChunkSize > 0 )
		{
			fileData.AlphaChunk = ReadChunk( binaryReader, fileData.AlphaChunkSize );
		}

		Data = DecodeImage( fileData );
	}

	private static TextureFileData ReadHeader( BinaryReader binaryReader )
	{
		var fileData = new TextureFileData()
		{
			Flags = (TextureFlags)binaryReader.ReadByte(),
			HasAlphaChannel = binaryReader.ReadByte() != 0,
			BitsPerPixel = binaryReader.ReadByte(),
			Version = binaryReader.ReadByte(),
			Width = binaryReader.ReadUInt16(),
			Height = binaryReader.ReadUInt16(),
			YChannelQuantizationScale = binaryReader.ReadUInt16(),
			CbChannelQuantizationScale = binaryReader.ReadUInt16(),
			CrChannelQuantizationScale = binaryReader.ReadUInt16(),
			AChannelQuantizationScale = binaryReader.ReadUInt16(),
			ColorChunkSize = binaryReader.ReadInt32(),
			AlphaChunkSize = binaryReader.ReadInt32(),
			Checksum = binaryReader.ReadInt32()
		};
		// 0x564C00 accepts codec 0x12 (BILZ/raw) and 0x13 (LZSS).
		// Bit zero selects the codec, not a half-resolution inverse transform.
		if ( fileData.Flags is not TextureFlags.Bilz and not TextureFlags.Lzss )
		{
			throw new InvalidDataException( "Unsupported WCT codec" );
		}

		if ( fileData.Width == 0 || fileData.Height == 0 || fileData.Width > 2048 || fileData.Height > 2048 )
		{
			throw new InvalidDataException( "Invalid WCT dimensions" );
		}

		if ( fileData.BitsPerPixel != 24 && fileData.BitsPerPixel != 32 )
		{
			throw new InvalidDataException( "Unsupported WCT pixel depth" );
		}

		fileData.HasAlphaChannel = fileData.BitsPerPixel == 32 && fileData.AlphaChunkSize > 0;
		if ( fileData.ColorChunkSize <= 0 || fileData.AlphaChunkSize < 0 )
		{
			throw new InvalidDataException( "Invalid WCT chunk sizes" );
		}

		return fileData;
	}

	private TextureData DecodeImage( TextureFileData fileData )
	{
		var alphaData = Array.Empty<byte>();
		var size = (int)GetAlignedSize( (uint)Math.Max( fileData.Height, fileData.Width ) );
		var colorData = Decompress( fileData.ColorChunk, fileData.Flags, size * size * 9 / 2 );
		if ( fileData.HasAlphaChannel )
		{
			alphaData = Decompress( fileData.AlphaChunk, fileData.Flags, size * size * 3 );
		}

		var outputY = Enumerable.Repeat( 0f, size * size ).ToList();
		var outputCb = Enumerable.Repeat( 0f, size * size ).ToList();
		var outputCr = Enumerable.Repeat( 0f, size * size ).ToList();
		var outputA = Enumerable.Repeat( 255f, size * size ).ToList();
		var state = new ImageDecodeState( size )
		{
			InterleavedRows = fileData.Flags == TextureFlags.Lzss
		};
		DecodeChannel( ref state, size, ref colorData, ref outputY, ComputeDequantizationScaleY( fileData.YChannelQuantizationScale ) );
		DecodeChannel( ref state, size / 2, ref colorData, ref outputCb, ComputeDequantizationScaleCbCr( fileData.CbChannelQuantizationScale ) );
		DecodeChannel( ref state, size / 2, ref colorData, ref outputCr, ComputeDequantizationScaleCbCr( fileData.CrChannelQuantizationScale ) );
		if ( fileData.HasAlphaChannel )
		{
			DecodeChannel( ref state, size, ref alphaData, ref outputA, ComputeDequantizationScaleA( fileData.AChannelQuantizationScale ) );
		}

		var outputBytes = new byte[fileData.Width * fileData.Height * 4];
		for ( int y = 0; y < fileData.Height; y++ )
		{
			for ( int x = 0; x < fileData.Width; x++ )
			{
				// Convert from ycbcr to rgb
				float cy = outputY[y * size + x];
				float cb = outputCb[((y / 2) * (size / 2) + (x / 2))];
				float cr = outputCr[((y / 2) * (size / 2) + (x / 2))];
				byte r, g, b;
				if ( state.InterleavedRows )
				{
					int iy = RoundedWord( cy );
					int icb = (sbyte)RoundedWord( cb ), icr = (sbyte)RoundedWord( cr );
					r = Saturate( iy + (int)(1.402 * icr) );
					g = Saturate( iy - (int)(0.344136 * icb) - (int)(0.714136 * icr) );
					b = Saturate( iy + (int)(1.772 * icb) );
				}
				else
				{
					r = ToColorByte( cy, (double)1.402f * cr );
					g = Saturate( RoundedWord( cy - (double)0.344136f * cb - (double)0.714136f * cr ) );
					b = ToColorByte( cy, (double)1.772f * cb );
				}

				float a = outputA[y * size + x];
				outputBytes[((y * fileData.Width + x) * 4)] = r;
				outputBytes[((y * fileData.Width + x) * 4) + 1] = g;
				outputBytes[((y * fileData.Width + x) * 4) + 2] = b;
				outputBytes[((y * fileData.Width + x) * 4) + 3] = Saturate( RoundedWord( a ) );
			}
		}

		return new TextureData( fileData.Width, fileData.Height, outputBytes );
	}

	private static int RoundedWord( double value ) => (short)BitConverter.SingleToInt32Bits( (float)(value + RoundingBias) );
	private static byte Saturate( int value ) => (byte)Math.Clamp( value, 0, 255 );

	// Codec 0x12 rounds the biased chroma contribution before adding luma,
	// then uses signed MMX saturation. Codec 0x13 uses integer lookup tables.
	private static byte ToColorByte( double luma, double chroma )
	{
		float biased = (float)(chroma + RoundingBias);
		return Saturate( (short)BitConverter.SingleToInt32Bits( (float)(luma + biased) ) );
	}

	private static byte[] ReadChunk( BinaryReader reader, int length )
	{
		return reader.ReadExactBytes( length, "Truncated WCT chunk" );
	}
}
