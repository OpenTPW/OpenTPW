using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>Decodes Electronic Arts TQI (<c>pIQT</c> chunks) intra-coded frames into planar YUV420p.</summary>
internal static class TqiVideoDecoder
{
	private static readonly int[] Mpeg1DefaultIntraMatrix =
	[
		 8, 16, 19, 22, 26, 27, 29, 34,
		16, 16, 22, 24, 27, 29, 34, 37,
		19, 22, 26, 27, 29, 34, 34, 38,
		22, 22, 26, 27, 29, 34, 37, 40,
		22, 26, 27, 29, 32, 35, 40, 48,
		26, 27, 29, 32, 35, 40, 48, 58,
		26, 27, 29, 34, 38, 46, 56, 69,
		27, 29, 35, 38, 46, 56, 69, 83,
	];

	// 16384 * s[r] * s[c] where s[k] = k % 4 == 0 ? 1 : sqrt(2) * cos(k * pi / 16).
	private static readonly int[] InvAanScales =
	[
		16384, 22725, 21407, 19266, 16384, 12873,  8867,  4520,
		22725, 31521, 29692, 26722, 22725, 17855, 12299,  6270,
		21407, 29692, 27969, 25172, 21407, 16819, 11585,  5906,
		19266, 26722, 25172, 22654, 19266, 15137, 10426,  5315,
		16384, 22725, 21407, 19266, 16384, 12873,  8867,  4520,
		12873, 17855, 16819, 15137, 12873, 10114,  6967,  3552,
		 8867, 12299, 11585, 10426,  8867,  6967,  4799,  2446,
		 4520,  6270,  5906,  5315,  4520,  3552,  2446,  1247,
	];

	private static readonly int[] ZigzagDirect =
	[
		 0,  1,  8, 16,  9,  2,  3, 10,
		17, 24, 32, 25, 18, 11,  4,  5,
		12, 19, 26, 33, 40, 48, 41, 34,
		27, 20, 13,  6,  7, 14, 21, 28,
		35, 42, 49, 56, 57, 50, 43, 36,
		29, 22, 15, 23, 30, 37, 44, 51,
		58, 59, 52, 45, 38, 31, 39, 46,
		53, 60, 61, 54, 47, 55, 62, 63,
	];

	private readonly record struct VlcEntry( int Code, int Length, int Run, int Level );

	private static readonly VlcEntry[] DcLumEntries =
	[
		new( 0b100, 3, 0, 0 ),
		new( 0b00, 2, 0, 1 ),
		new( 0b01, 2, 0, 2 ),
		new( 0b101, 3, 0, 3 ),
		new( 0b110, 3, 0, 4 ),
		new( 0b1110, 4, 0, 5 ),
		new( 0b11110, 5, 0, 6 ),
		new( 0b111110, 6, 0, 7 ),
		new( 0b1111110, 7, 0, 8 ),
		new( 0b11111110, 8, 0, 9 ),
		new( 0b111111110, 9, 0, 10 ),
		new( 0b111111111, 9, 0, 11 ),
	];

	private static readonly VlcEntry[] DcChromaEntries =
	[
		new( 0b00, 2, 0, 0 ),
		new( 0b01, 2, 0, 1 ),
		new( 0b10, 2, 0, 2 ),
		new( 0b110, 3, 0, 3 ),
		new( 0b1110, 4, 0, 4 ),
		new( 0b11110, 5, 0, 5 ),
		new( 0b111110, 6, 0, 6 ),
		new( 0b1111110, 7, 0, 7 ),
		new( 0b11111110, 8, 0, 8 ),
		new( 0b111111110, 9, 0, 9 ),
		new( 0b1111111110, 10, 0, 10 ),
		new( 0b1111111111, 10, 0, 11 ),
	];

	// ISO 11172-2 Table B-14; Run = -1 marks EOB, Run = -2 marks ESCAPE.
	private static readonly VlcEntry[] Mpeg1B14Entries =
	[
		new( 0b10, 2, -1, 0 ),
		new( 0b000001, 6, -2, 0 ),
		new( 0b11, 2, 0, 1 ),
		new( 0b011, 3, 1, 1 ),
		new( 0b0100, 4, 0, 2 ),
		new( 0b0101, 4, 2, 1 ),
		new( 0b00101, 5, 0, 3 ),
		new( 0b00111, 5, 3, 1 ),
		new( 0b00110, 5, 4, 1 ),
		new( 0b000110, 6, 1, 2 ),
		new( 0b000111, 6, 5, 1 ),
		new( 0b000101, 6, 6, 1 ),
		new( 0b000100, 6, 7, 1 ),
		new( 0b0000110, 7, 0, 4 ),
		new( 0b0000100, 7, 2, 2 ),
		new( 0b0000111, 7, 8, 1 ),
		new( 0b0000101, 7, 9, 1 ),
		new( 0b00100110, 8, 0, 5 ),
		new( 0b00100001, 8, 0, 6 ),
		new( 0b00100101, 8, 1, 3 ),
		new( 0b00100100, 8, 3, 2 ),
		new( 0b00100111, 8, 10, 1 ),
		new( 0b00100011, 8, 11, 1 ),
		new( 0b00100010, 8, 12, 1 ),
		new( 0b00100000, 8, 13, 1 ),
		new( 0b0000001010, 10, 0, 7 ),
		new( 0b0000001100, 10, 1, 4 ),
		new( 0b0000001011, 10, 2, 3 ),
		new( 0b0000001111, 10, 4, 2 ),
		new( 0b0000001001, 10, 5, 2 ),
		new( 0b0000001110, 10, 14, 1 ),
		new( 0b0000001101, 10, 15, 1 ),
		new( 0b0000001000, 10, 16, 1 ),
		new( 0b000000011101, 12, 0, 8 ),
		new( 0b000000011000, 12, 0, 9 ),
		new( 0b000000010011, 12, 0, 10 ),
		new( 0b000000010000, 12, 0, 11 ),
		new( 0b000000011011, 12, 1, 5 ),
		new( 0b000000010100, 12, 2, 4 ),
		new( 0b000000011100, 12, 3, 3 ),
		new( 0b000000010010, 12, 4, 3 ),
		new( 0b000000011110, 12, 6, 2 ),
		new( 0b000000010101, 12, 7, 2 ),
		new( 0b000000010001, 12, 8, 2 ),
		new( 0b000000011111, 12, 17, 1 ),
		new( 0b000000011010, 12, 18, 1 ),
		new( 0b000000011001, 12, 19, 1 ),
		new( 0b000000010111, 12, 20, 1 ),
		new( 0b000000010110, 12, 21, 1 ),
		new( 0b0000000011010, 13, 0, 12 ),
		new( 0b0000000011001, 13, 0, 13 ),
		new( 0b0000000011000, 13, 0, 14 ),
		new( 0b0000000010111, 13, 0, 15 ),
		new( 0b0000000010110, 13, 1, 6 ),
		new( 0b0000000010101, 13, 1, 7 ),
		new( 0b0000000010100, 13, 2, 5 ),
		new( 0b0000000010011, 13, 3, 4 ),
		new( 0b0000000010010, 13, 5, 3 ),
		new( 0b0000000010001, 13, 9, 2 ),
		new( 0b0000000010000, 13, 10, 2 ),
		new( 0b0000000011111, 13, 22, 1 ),
		new( 0b0000000011110, 13, 23, 1 ),
		new( 0b0000000011101, 13, 24, 1 ),
		new( 0b0000000011100, 13, 25, 1 ),
		new( 0b0000000011011, 13, 26, 1 ),
		new( 0b00000000011111, 14, 0, 16 ),
		new( 0b00000000011110, 14, 0, 17 ),
		new( 0b00000000011101, 14, 0, 18 ),
		new( 0b00000000011100, 14, 0, 19 ),
		new( 0b00000000011011, 14, 0, 20 ),
		new( 0b00000000011010, 14, 0, 21 ),
		new( 0b00000000011001, 14, 0, 22 ),
		new( 0b00000000011000, 14, 0, 23 ),
		new( 0b00000000010111, 14, 0, 24 ),
		new( 0b00000000010110, 14, 0, 25 ),
		new( 0b00000000010101, 14, 0, 26 ),
		new( 0b00000000010100, 14, 0, 27 ),
		new( 0b00000000010011, 14, 0, 28 ),
		new( 0b00000000010010, 14, 0, 29 ),
		new( 0b00000000010001, 14, 0, 30 ),
		new( 0b00000000010000, 14, 0, 31 ),
		new( 0b000000000010000, 15, 0, 40 ),
		new( 0b000000000010001, 15, 0, 39 ),
		new( 0b000000000010010, 15, 0, 38 ),
		new( 0b000000000010011, 15, 0, 37 ),
		new( 0b000000000010100, 15, 0, 36 ),
		new( 0b000000000010101, 15, 0, 35 ),
		new( 0b000000000010110, 15, 0, 34 ),
		new( 0b000000000010111, 15, 0, 33 ),
		new( 0b000000000011000, 15, 0, 32 ),
		new( 0b000000000011001, 15, 1, 14 ),
		new( 0b000000000011010, 15, 1, 13 ),
		new( 0b000000000011011, 15, 1, 12 ),
		new( 0b000000000011100, 15, 1, 11 ),
		new( 0b000000000011101, 15, 1, 10 ),
		new( 0b000000000011110, 15, 1, 9 ),
		new( 0b000000000011111, 15, 1, 8 ),
		new( 0b0000000000010000, 16, 1, 18 ),
		new( 0b0000000000010001, 16, 1, 17 ),
		new( 0b0000000000010010, 16, 1, 16 ),
		new( 0b0000000000010011, 16, 1, 15 ),
		new( 0b0000000000010100, 16, 6, 3 ),
		new( 0b0000000000010101, 16, 16, 2 ),
		new( 0b0000000000010110, 16, 15, 2 ),
		new( 0b0000000000010111, 16, 14, 2 ),
		new( 0b0000000000011000, 16, 13, 2 ),
		new( 0b0000000000011001, 16, 12, 2 ),
		new( 0b0000000000011010, 16, 11, 2 ),
		new( 0b0000000000011011, 16, 31, 1 ),
		new( 0b0000000000011100, 16, 30, 1 ),
		new( 0b0000000000011101, 16, 29, 1 ),
		new( 0b0000000000011110, 16, 28, 1 ),
		new( 0b0000000000011111, 16, 27, 1 ),
	];

	private readonly struct VlcNode
	{
		public readonly int Zero;
		public readonly int One;
		public readonly int Run;
		public readonly int Level;
		public readonly bool IsLeaf;

		public VlcNode( int zero, int one )
		{
			Zero = zero;
			One = one;
			Run = 0;
			Level = 0;
			IsLeaf = false;
		}

		public VlcNode( int run, int level, bool isLeaf )
		{
			Zero = -1;
			One = -1;
			Run = run;
			Level = level;
			IsLeaf = isLeaf;
		}
	}

	private static readonly VlcNode[] DcLumTree = BuildTree( DcLumEntries );
	private static readonly VlcNode[] DcChromaTree = BuildTree( DcChromaEntries );
	private static readonly VlcNode[] B14Tree = BuildTree( Mpeg1B14Entries );

	private static VlcNode[] BuildTree( VlcEntry[] entries )
	{
		var nodes = new List<(int Zero, int One, int Run, int Level, bool IsLeaf)> { (-1, -1, 0, 0, false) };
		foreach ( var entry in entries )
		{
			int cur = 0;
			for ( int bitPos = entry.Length - 1; bitPos >= 0; bitPos-- )
			{
				int bit = (entry.Code >> bitPos) & 1;
				int next = bit == 0 ? nodes[cur].Zero : nodes[cur].One;
				if ( next == -1 )
				{
					next = nodes.Count;
					if ( bitPos == 0 )
						nodes.Add( (-1, -1, entry.Run, entry.Level, true) );
					else
						nodes.Add( (-1, -1, 0, 0, false) );

					var parent = nodes[cur];
					if ( bit == 0 )
						nodes[cur] = (next, parent.One, parent.Run, parent.Level, parent.IsLeaf);
					else
						nodes[cur] = (parent.Zero, next, parent.Run, parent.Level, parent.IsLeaf);
				}
				cur = next;
			}
		}

		var result = new VlcNode[nodes.Count];
		for ( int i = 0; i < nodes.Count; i++ )
		{
			var n = nodes[i];
			result[i] = n.IsLeaf ? new VlcNode( n.Run, n.Level, true ) : new VlcNode( n.Zero, n.One );
		}
		return result;
	}

	public static TgqVideoFrame DecodeFrame( ReadOnlySpan<byte> payload )
	{
		if ( payload.Length < 8 )
			throw new InvalidDataException( "Truncated pIQT frame header." );

		int width = BinaryPrimitives.ReadUInt16LittleEndian( payload );
		int height = BinaryPrimitives.ReadUInt16LittleEndian( payload.Slice( 2, 2 ) );
		byte quant = payload[4];
		byte mbWidth = payload[5];
		byte mbHeight = payload[6];

		var qtable = CalculateQTable( quant );
		var reader = new BitReader32Le( payload[8..] );

		var yPlane = new byte[width * height];
		int chromaWidth = width / 2;
		int chromaHeight = height / 2;
		var uPlane = new byte[chromaWidth * chromaHeight];
		var vPlane = new byte[chromaWidth * chromaHeight];

		Span<int> lastDc = stackalloc int[3];
		Span<int> block = stackalloc int[64];
		Span<int> workspace = stackalloc int[64];

		for ( int mbY = 0; mbY < mbHeight; mbY++ )
		{
			for ( int mbX = 0; mbX < mbWidth; mbX++ )
			{
				for ( int n = 0; n < 6; n++ )
				{
					block.Clear();
					int comp = n < 4 ? 0 : (n == 4 ? 1 : 2);
					int dcDiff = DecodeDc( ref reader, n < 4 ? DcLumTree : DcChromaTree );
					lastDc[comp] += dcDiff;
					block[0] = lastDc[comp] * qtable[0];

					int idx = 0;
					while ( true )
					{
						var (run, level) = ReadVlc( ref reader, B14Tree );
						if ( run == -1 )
							break;

						if ( run == -2 )
						{
							run = reader.ReadBits( 6 );
							int lvl = reader.ReadBits( 8 );
							if ( lvl == 0 )
								level = reader.ReadBits( 8 );
							else if ( lvl == 128 )
								level = reader.ReadBits( 8 ) - 256;
							else
								level = lvl >= 128 ? lvl - 256 : lvl;
						}
						else if ( reader.ReadBit() != 0 )
						{
							level = -level;
						}

						idx += run + 1;
						if ( idx >= 64 )
							throw new InvalidDataException( $"TQI coefficient index out of range ({idx}) at macroblock ({mbX},{mbY})." );

						int z = ZigzagDirect[idx];
						block[z] = level * qtable[z];
					}

					if ( n < 4 )
					{
						int bx = mbX * 16 + (n & 1) * 8;
						int by = mbY * 16 + (n >> 1) * 8;
						EaIdctPut( yPlane, width, height, bx, by, block, workspace );
					}
					else if ( n == 4 )
					{
						EaIdctPut( uPlane, chromaWidth, chromaHeight, mbX * 8, mbY * 8, block, workspace );
					}
					else
					{
						EaIdctPut( vPlane, chromaWidth, chromaHeight, mbX * 8, mbY * 8, block, workspace );
					}
				}
			}
		}

		return new TgqVideoFrame( width, height, yPlane, uPlane, vPlane );
	}

	private static int[] CalculateQTable( byte quant )
	{
		int qscale = (215 - 2 * quant) * 5;
		var qtable = new int[64];
		qtable[0] = (InvAanScales[0] * Mpeg1DefaultIntraMatrix[0]) >> 11;
		for ( int i = 1; i < 64; i++ )
			qtable[i] = (InvAanScales[i] * Mpeg1DefaultIntraMatrix[i] * qscale + 32) >> 18;
		return qtable;
	}

	private static int DecodeDc( ref BitReader32Le reader, VlcNode[] tree )
	{
		int size = ReadVlc( ref reader, tree ).Level;
		if ( size == 0 )
			return 0;

		int val = reader.ReadBits( size );
		if ( (val & (1 << (size - 1))) == 0 )
			val -= (1 << size) - 1;
		return val;
	}

	private static (int Run, int Level) ReadVlc( ref BitReader32Le reader, VlcNode[] tree )
	{
		int node = 0;
		while ( !tree[node].IsLeaf )
		{
			int bit = reader.ReadBit();
			node = bit == 0 ? tree[node].Zero : tree[node].One;
			if ( node < 0 )
				throw new InvalidDataException( "Invalid VLC code in TQI bitstream." );
		}
		return (tree[node].Run, tree[node].Level);
	}

	private static void EaIdctPut( byte[] dest, int planeWidth, int planeHeight, int bx, int by, ReadOnlySpan<int> block, Span<int> tmp )
	{
		const int aSqrt = 181;
		const int a4 = 669;
		const int a2 = 277;
		const int a5 = 196;

		for ( int r = 0; r < 8; r++ )
		{
			int row = r * 8;
			int s0 = block[row + 0], s1 = block[row + 1], s2 = block[row + 2], s3 = block[row + 3];
			int s4 = block[row + 4], s5 = block[row + 5], s6 = block[row + 6], s7 = block[row + 7];

			int a0 = s0 + s4;
			int a1 = s0 - s4;
			int a2v = s2 + s6;
			int a3 = (((s2 - s6) * aSqrt) >> 7) - a2v;

			int b0 = a0 + a2v;
			int b1 = a1 + a3;
			int b2 = a1 - a3;
			int b3 = a0 - a2v;

			int c0 = s5 + s3;
			int c1 = s5 - s3;
			int c2 = s1 + s7;
			int c3 = s1 - s7;

			int e7 = c2 + c0;
			int e11 = ((c2 - c0) * aSqrt) >> 7;
			int z5 = ((c3 + c1) * a5) >> 8;
			int e10 = z5 + ((c1 * a2) >> 8);
			int e12 = ((c3 * a4) >> 8) - z5;
			int e6 = e12 - e7;
			int e5 = e11 - e6;
			int e4 = e10 - e5;

			tmp[row + 0] = b0 + e7;
			tmp[row + 7] = b0 - e7;
			tmp[row + 1] = b1 + e6;
			tmp[row + 6] = b1 - e6;
			tmp[row + 2] = b2 + e5;
			tmp[row + 5] = b2 - e5;
			tmp[row + 3] = b3 + e4;
			tmp[row + 4] = b3 - e4;
		}

		for ( int c = 0; c < 8; c++ )
		{
			int x = bx + c;
			if ( x >= planeWidth )
				continue;

			int s0 = tmp[0 * 8 + c], s1 = tmp[1 * 8 + c], s2 = tmp[2 * 8 + c], s3 = tmp[3 * 8 + c];
			int s4 = tmp[4 * 8 + c], s5 = tmp[5 * 8 + c], s6 = tmp[6 * 8 + c], s7 = tmp[7 * 8 + c];

			int a0 = s0 + s4;
			int a1 = s0 - s4;
			int a2v = s2 + s6;
			int a3 = (((s2 - s6) * aSqrt) >> 7) - a2v;

			int b0 = a0 + a2v;
			int b1 = a1 + a3;
			int b2 = a1 - a3;
			int b3 = a0 - a2v;

			int c0 = s5 + s3;
			int c1 = s5 - s3;
			int c2 = s1 + s7;
			int c3 = s1 - s7;

			int e7 = c2 + c0;
			int e11 = ((c2 - c0) * aSqrt) >> 7;
			int z5 = ((c3 + c1) * a5) >> 8;
			int e10 = z5 + ((c1 * a2) >> 8);
			int e12 = ((c3 * a4) >> 8) - z5;
			int e6 = e12 - e7;
			int e5 = e11 - e6;
			int e4 = e10 - e5;

			WriteSample( dest, planeWidth, planeHeight, x, by + 0, (b0 + e7 + 32) >> 6 );
			WriteSample( dest, planeWidth, planeHeight, x, by + 1, (b1 + e6 + 32) >> 6 );
			WriteSample( dest, planeWidth, planeHeight, x, by + 2, (b2 + e5 + 32) >> 6 );
			WriteSample( dest, planeWidth, planeHeight, x, by + 3, (b3 + e4 + 32) >> 6 );
			WriteSample( dest, planeWidth, planeHeight, x, by + 4, (b3 - e4 + 32) >> 6 );
			WriteSample( dest, planeWidth, planeHeight, x, by + 5, (b2 - e5 + 32) >> 6 );
			WriteSample( dest, planeWidth, planeHeight, x, by + 6, (b1 - e6 + 32) >> 6 );
			WriteSample( dest, planeWidth, planeHeight, x, by + 7, (b0 - e7 + 32) >> 6 );
		}
	}

	private static void WriteSample( byte[] dest, int planeWidth, int planeHeight, int x, int y, int value )
	{
		if ( y < planeHeight )
			dest[y * planeWidth + x] = (byte)Math.Clamp( value, 0, 255 );
	}

	// EA TQI packs its bitstream into 32-bit little-endian words, read MSB-first within each word.
	private ref struct BitReader32Le
	{
		private readonly ReadOnlySpan<byte> data;
		private int byteOffset;
		private uint currentWord;
		private int bitsRemaining;

		public BitReader32Le( ReadOnlySpan<byte> data )
		{
			this.data = data;
			byteOffset = 0;
			currentWord = 0;
			bitsRemaining = 0;
		}

		public int ReadBit()
		{
			if ( bitsRemaining == 0 )
				Refill();

			bitsRemaining--;
			return (int)((currentWord >> bitsRemaining) & 1);
		}

		public int ReadBits( int count )
		{
			int value = 0;
			for ( int i = 0; i < count; i++ )
				value = (value << 1) | ReadBit();
			return value;
		}

		private void Refill()
		{
			if ( byteOffset + 4 <= data.Length )
			{
				currentWord = BinaryPrimitives.ReadUInt32LittleEndian( data.Slice( byteOffset, 4 ) );
			}
			else if ( byteOffset < data.Length )
			{
				Span<byte> tail = stackalloc byte[4];
				data[byteOffset..].CopyTo( tail );
				currentWord = BinaryPrimitives.ReadUInt32LittleEndian( tail );
			}
			else
			{
				currentWord = 0;
			}

			byteOffset += 4;
			bitsRemaining = 32;
		}
	}
}
