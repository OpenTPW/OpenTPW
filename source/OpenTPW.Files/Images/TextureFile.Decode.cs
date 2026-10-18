namespace OpenTPW;

partial class TextureFile
{
	private static int ReadCoefficient( byte[] source, ref int index )
	{
		if ( index >= source.Length )
		{
			throw new InvalidDataException( "Truncated WCT coefficients" );
		}

		int value = (sbyte)source[index++];
		if ( value != -128 )
		{
			return value;
		}

		if ( source.Length - index < 2 )
		{
			throw new InvalidDataException( "Truncated WCT escaped coefficient" );
		}

		value = (short)(source[index] | source[index + 1] << 8);
		index += 2;
		return value;
	}

	private uint GetAlignedSize( uint size )
	{
		uint value = (size - 1);
		uint n = 1;
		for ( ; (value >>= 1) != 0; )
		{
			++n;
		}

		if ( n < 3 )
		{
			n = 3;
		}

		return (uint)(1 << (int)n);
	}

	static float ApplyScalingCoefficientsInv( float smooth0, float coef0, float smooth1, float coef1, D4Coefficients coefs, bool round )
	{
		double value = (double)smooth0 * coefs.IH0 + (double)coef0 * coefs.IH1 + (double)smooth1 * coefs.IH2 + (double)coef1 * coefs.IH3;
		return round ? (float)(value + RoundingBias) - (float)RoundingBias : (float)value;
	}

	static float ApplyWaveCoefficientsInv( float smooth0, float coef0, float smooth1, float coef1, D4Coefficients coefs, bool round )
	{
		double value = (double)smooth0 * coefs.IG0 + (double)coef0 * coefs.IG1 + (double)smooth1 * coefs.IG2 + (double)coef1 * coefs.IG3;
		return round ? (float)(value + RoundingBias) - (float)RoundingBias : (float)value;
	}

	void D4InverseTransform( Func<int, D4Component, int> indexLookup,
		Func<int, D4Component, int> outputIndexLookup,
		ref List<float> pSrc,
		ref List<float> pDest,
		int offset,
		int size,
		D4Coefficients coefs,
		bool round = false )
	{
		for ( int i = 0; i < size; i++ )
		{
			float s0, w0, s1, w1;
			int s0i, w0i, s1i, w1i;
			if ( i == 0 )
			{
				s0i = indexLookup( size - 1, D4Component.Scale );
				w0i = indexLookup( size - 1, D4Component.Wavelet );
			}
			else
			{
				s0i = indexLookup( i - 1, D4Component.Scale );
				w0i = indexLookup( i - 1, D4Component.Wavelet );
			}

			s1i = indexLookup( i, D4Component.Scale );
			w1i = indexLookup( i, D4Component.Wavelet );
			s0 = pSrc[s0i + offset];
			w0 = pSrc[w0i + offset];
			s1 = pSrc[s1i + offset];
			w1 = pSrc[w1i + offset];
			int s0d = outputIndexLookup( i, D4Component.Scale );
			int w0d = outputIndexLookup( i, D4Component.Wavelet );
			pDest[s0d + offset] = ApplyScalingCoefficientsInv( s0, w0, s1, w1, coefs, round );
			pDest[w0d + offset] = ApplyWaveCoefficientsInv( s0, w0, s1, w1, coefs, round );
		}
	}

	private bool DecodeChannel( ref ImageDecodeState state, int size, ref byte[] pSrc, ref List<float> outputBuffer, float dequantizationScale )
	{
		int count = size * (size / 2);
		int index = 0;
		//
		// Step 1: Dequantize
		//
		for ( int i = 0; i < count; i++ )
		{
			int val = ReadCoefficient( pSrc, ref index );
			state.DequantizationBuffer[i * 2] = state.InterleavedRows ? val : (float)val * dequantizationScale;
			val = ReadCoefficient( pSrc, ref index );
			state.DequantizationBuffer[i * 2 + 1] = state.InterleavedRows ? val : (float)val * dequantizationScale;
		}
		// Advance the actual remaining length, so a truncated Cb/Cr channel
		// cannot read stale coefficients left behind by an in-place shift.
		pSrc = pSrc[index..];
		//
		// Step 2: decode rows
		//
		D4Coefficients coefs = new( state.InterleavedRows ? dequantizationScale : 1.0f );
		bool interleavedRows = state.InterleavedRows;
		for ( int i = 0; i < size; i++ )
		{
			D4InverseTransform(// Index lookup
			( index, component ) => {
				// 0x592C50: smooth coefficients occupy the first half of a row.
				return interleavedRows ? index * 2 + (component == D4Component.Wavelet ? 1 : 0) : index + (component == D4Component.Wavelet ? size / 2 : 0);
			},
			// Output index lookup
			( index, component ) => {
				return index * 2 + (component == D4Component.Wavelet ? 1 : 0);
			}, ref state.DequantizationBuffer, ref state.RowDecodeBuffer, i * size, size / 2, coefs );
		}
		//
		// Step 3: decode columns
		//
		coefs = new( 1.0f );
		for ( int i = 0; i < size; ++i )
		{
			int sOffset = 0;
			int wOffset = size * (size / 2);
			int colOffset = i;
			D4InverseTransform(// Index lookup
			( index, component ) => {
				int baseIndex = index * size;
				return component switch
				{
					D4Component.Scale => baseIndex + colOffset + sOffset,
					D4Component.Wavelet => baseIndex + colOffset + wOffset,
					_ => throw new NotImplementedException(),
				};
			},
			// Output index lookup
			( index, component ) => {
				int baseIndex = index * 2 * size + colOffset;
				return component switch
				{
					D4Component.Scale => baseIndex,
					D4Component.Wavelet => baseIndex + size,
					_ => throw new NotImplementedException(),
				};
			}, ref state.RowDecodeBuffer, ref outputBuffer, 0, size / 2, coefs, state.InterleavedRows );
		}

		return true;
	}
}
