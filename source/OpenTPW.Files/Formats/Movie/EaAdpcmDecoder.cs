using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>Decodes Electronic Arts ADPCM (<c>SCDl</c> chunks, codec type 7) into 16-bit interleaved PCM.</summary>
internal static class EaAdpcmDecoder
{
	// EA ADPCM filter table: indices 0..3 hold coeff1, indices 4..7 hold coeff2 (fixed-point Q8).
	private static readonly int[] EaTable = [0, 0xF0, 0x1CC, 0x188, 0, 0, -0xD0, -0xDC];

	public static void DecodeChunk( ReadOnlySpan<byte> payload, int channels, List<short> output )
	{
		if ( channels == 2 )
			DecodeStereo( payload, output );
		else if ( channels == 1 )
			DecodeMono( payload, output );
		else
			throw new InvalidDataException( $"Unsupported EA ADPCM channel count: {channels}." );
	}

	private static void DecodeStereo( ReadOnlySpan<byte> payload, List<short> output )
	{
		if ( payload.Length < 12 )
			return;

		int sampleCount = BinaryPrimitives.ReadInt32LittleEndian( payload );
		if ( sampleCount <= 0 )
			return;

		int c1L = BinaryPrimitives.ReadInt16LittleEndian( payload.Slice( 4, 2 ) );
		int c2L = BinaryPrimitives.ReadInt16LittleEndian( payload.Slice( 6, 2 ) );
		int c1R = BinaryPrimitives.ReadInt16LittleEndian( payload.Slice( 8, 2 ) );
		int c2R = BinaryPrimitives.ReadInt16LittleEndian( payload.Slice( 10, 2 ) );

		// Retail game.exe (TPI 0x861373..0x8613a4) computes complete 30-byte sub-blocks as (payload.Length - 12) / 30
		// and decodes 28 stereo sample pairs per complete sub-block, ignoring any trailing partial sub-block.
		int completeSubBlocks = (payload.Length - 12) / 30;
		int maxSamples = Math.Min( sampleCount, completeSubBlocks * 28 );
		int offset = 12;
		int remaining = maxSamples;
		while ( remaining > 0 )
		{
			byte coeffByte = payload[offset++];
			byte shiftByte = payload[offset++];

			int coeff1L = EaTable[(coeffByte >> 4) & 0x03];
			int coeff2L = EaTable[((coeffByte >> 4) & 0x03) + 4];
			int coeff1R = EaTable[coeffByte & 0x03];
			int coeff2R = EaTable[(coeffByte & 0x03) + 4];

			int shiftL = 20 - ((shiftByte >> 4) & 0x0F);
			int shiftR = 20 - (shiftByte & 0x0F);

			int subCount = Math.Min( 28, remaining );
			for ( int i = 0; i < subCount; i++ )
			{
				byte packed = payload[offset++];
				int nL = SignExtendNibble( packed >> 4 );
				int nR = SignExtendNibble( packed & 0x0F );

				int sL = Clamp16( ((nL << shiftL) + c1L * coeff1L + c2L * coeff2L + 0x80) >> 8 );
				c2L = c1L;
				c1L = sL;

				int sR = Clamp16( ((nR << shiftR) + c1R * coeff1R + c2R * coeff2R + 0x80) >> 8 );
				c2R = c1R;
				c1R = sR;

				output.Add( (short)sL );
				output.Add( (short)sR );
			}

			remaining -= subCount;
		}
	}

	private static void DecodeMono( ReadOnlySpan<byte> payload, List<short> output )
	{
		if ( payload.Length < 8 )
			return;

		int sampleCount = BinaryPrimitives.ReadInt32LittleEndian( payload );
		if ( sampleCount <= 0 )
			return;

		int c1 = BinaryPrimitives.ReadInt16LittleEndian( payload.Slice( 4, 2 ) );
		int c2 = BinaryPrimitives.ReadInt16LittleEndian( payload.Slice( 6, 2 ) );

		// Analogous to stereo (TPI 0x8613ef..0x86143b): 15 bytes per complete mono sub-block (1 header + 14 packed bytes = 28 samples).
		int completeSubBlocks = (payload.Length - 8) / 15;
		int maxSamples = Math.Min( sampleCount, completeSubBlocks * 28 );
		int offset = 8;
		int remaining = maxSamples;
		while ( remaining > 0 )
		{
			byte header = payload[offset++];
			int coeff1 = EaTable[(header >> 4) & 0x03];
			int coeff2 = EaTable[((header >> 4) & 0x03) + 4];
			int shift = 20 - (header & 0x0F);

			int subCount = Math.Min( 28, remaining );
			int pairs = (subCount + 1) / 2;
			int emitted = 0;
			for ( int i = 0; i < pairs && offset < payload.Length; i++ )
			{
				byte packed = payload[offset++];
				int n0 = SignExtendNibble( packed >> 4 );
				int s0 = Clamp16( ((n0 << shift) + c1 * coeff1 + c2 * coeff2 + 0x80) >> 8 );
				c2 = c1;
				c1 = s0;
				output.Add( (short)s0 );
				emitted++;

				if ( emitted < subCount )
				{
					int n1 = SignExtendNibble( packed & 0x0F );
					int s1 = Clamp16( ((n1 << shift) + c1 * coeff1 + c2 * coeff2 + 0x80) >> 8 );
					c2 = c1;
					c1 = s1;
					output.Add( (short)s1 );
					emitted++;
				}
			}

			remaining -= subCount;
		}
	}

	private static int SignExtendNibble( int n ) => (n & 0x08) != 0 ? (n | ~0x0F) : (n & 0x0F);

	private static int Clamp16( int v ) => Math.Clamp( v, short.MinValue, short.MaxValue );
}
