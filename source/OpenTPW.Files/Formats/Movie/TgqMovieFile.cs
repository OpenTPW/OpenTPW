using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>One decoded YUV420p video frame from an EA <c>.tgq</c> (<c>pIQT</c>) movie.</summary>
public sealed class TgqVideoFrame
{
	public int Width { get; }
	public int Height { get; }
	public byte[] Y { get; }
	public byte[] U { get; }
	public byte[] V { get; }

	public TgqVideoFrame( int width, int height, byte[] y, byte[] u, byte[] v )
	{
		Width = width;
		Height = height;
		Y = y;
		U = u;
		V = v;
	}

	/// <summary>Converts this YUV420p frame to packed RGBA8888 (alpha = 255) the way the games' movie player does; see <see cref="WriteRgba"/>.</summary>
	public byte[] ToRgba()
	{
		var rgba = new byte[checked(Width * Height * 4)];
		WriteRgba( rgba );
		return rgba;
	}

	/// <summary>Writes packed RGBA8888 pixels into <paramref name="destination"/>.</summary>
	/// <remarks>
	/// Matches the player's lookup tables (<c>Tqi_BuildYuvTables16</c>, US 0x66fc10 / TPI 0x862680, and the 32-bit
	/// <c>Tqi_BuildYuvTables32</c>, US 0x66ffa0 / TPI 0x862a10): one 128-entry table per plane, indexed by the sample
	/// halved, each entry holding the odd value <c>2i+1</c> and its JFIF full-range contribution in 16.16 fixed point
	/// (<c>0x166e9</c> = 1.402, <c>-0x5816</c> = -0.344, <c>-0xb6cf</c> = -0.714, <c>0x1c5a1</c> = 1.772), so luma gets
	/// no 255/219 gain. The 16 subtracted from luma is not a constant in those tables; it was measured from the retail
	/// TPI first-run capture of <c>bf.tgq</c>, where about 97,000 flat pixels sit 14-15 below the table value at every
	/// level from 74 to 233 while chroma matches exactly.
	/// </remarks>
	public void WriteRgba( Span<byte> destination )
	{
		int required = checked(Width * Height * 4);
		if ( destination.Length < required )
			throw new ArgumentException( $"Destination buffer must be at least {required} bytes.", nameof(destination) );

		int chromaWidth = Width / 2;
		int dst = 0;
		for ( int row = 0; row < Height; row++ )
		{
			int yRow = row * Width;
			int uvRow = (row >> 1) * chromaWidth;
			for ( int col = 0; col < Width; col++ )
			{
				int y = TableValue( Math.Max( Y[yRow + col] - 16, 0 ) );
				int u = TableValue( U[uvRow + (col >> 1)] - 128 );
				int v = TableValue( V[uvRow + (col >> 1)] - 128 );

				int r = y + Fixed16( 0x166e9 * v );
				int g = y + Fixed16( -0x5816 * u ) + Fixed16( -0xb6cf * v );
				int b = y + Fixed16( 0x1c5a1 * u );

				destination[dst++] = (byte)Math.Clamp( r, 0, 255 );
				destination[dst++] = (byte)Math.Clamp( g, 0, 255 );
				destination[dst++] = (byte)Math.Clamp( b, 0, 255 );
				destination[dst++] = 255;
			}
		}
	}

	// The tables keep 7 bits per sample: index = sample >> 1, entry = 2 * index + 1.
	private static int TableValue( int sample ) => ((sample >> 1) << 1) + 1;

	// imul / shl edx,16 / shr eax,16 / adc: a 16.16 product rounded half up.
	private static int Fixed16( int product ) => (product + 0x8000) >> 16;
}

/// <summary>
/// Indexes Electronic Arts <c>.tgq</c> movie containers (<c>SCHl</c>/<c>pIQT</c>/<c>SCDl</c>)
/// and decodes <c>pIQT</c> video frames on demand so long movies do not pin hundreds of MB in RAM.
/// </summary>
public sealed class TgqMovieFile
{
	private const uint TagSchl = 0x6C484353; // "SCHl"
	private const uint TagSccl = 0x6C434353; // "SCCl"
	private const uint TagScdl = 0x6C444353; // "SCDl"
	private const uint TagScel = 0x6C454353; // "SCEl"
	private const uint TagPiqt = 0x54514970; // "pIQT"

	private readonly byte[] data;
	private readonly (int Offset, int Length)[] frameChunks;

	public int Width { get; }
	public int Height { get; }
	public int FrameRate { get; }
	public int SampleRate { get; }
	public int Channels { get; }
	public int FrameCount => frameChunks.Length;
	public short[] AudioSamples { get; }

	private TgqMovieFile( byte[] data, (int Offset, int Length)[] frameChunks,
		int width, int height, int frameRate, int sampleRate, int channels, short[] audioSamples )
	{
		this.data = data;
		this.frameChunks = frameChunks;
		Width = width;
		Height = height;
		FrameRate = frameRate;
		SampleRate = sampleRate;
		Channels = channels;
		AudioSamples = audioSamples;
	}

	/// <summary>Decodes a single <c>pIQT</c> intra-frame on demand.</summary>
	public TgqVideoFrame DecodeFrame( int frameIndex )
	{
		if ( (uint)frameIndex >= (uint)frameChunks.Length )
			throw new ArgumentOutOfRangeException( nameof(frameIndex) );

		var (offset, length) = frameChunks[frameIndex];
		return TqiVideoDecoder.DecodeFrame( data.AsSpan( offset, length ) );
	}

	/// <summary>Converts the decoded 16-bit PCM stream into normalized <c>[-1, 1]</c> float samples.</summary>
	public float[] GetAudioFloat()
	{
		var samples = new float[AudioSamples.Length];
		for ( int i = 0; i < AudioSamples.Length; i++ )
			samples[i] = AudioSamples[i] / 32768f;
		return samples;
	}

	public static TgqMovieFile Read( string path )
	{
		ArgumentException.ThrowIfNullOrEmpty( path );
		return Read( File.ReadAllBytes( path ) );
	}

	public static TgqMovieFile Read( Stream stream )
	{
		ArgumentNullException.ThrowIfNull( stream );
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		return Read( ms.ToArray() );
	}

	public static TgqMovieFile Read( byte[] data )
	{
		ArgumentNullException.ThrowIfNull( data );
		return ReadCore( data );
	}

	public static TgqMovieFile Read( ReadOnlySpan<byte> data ) => ReadCore( data.ToArray() );

	private static TgqMovieFile ReadCore( byte[] data )
	{
		// EA TGQ/PT defaults when optional PT header tags are omitted:
		// - 15 fps: EA TGQ container default when tag 0x1B is absent (all 5 retail TPW/TPI movies specify 0x1B: 30 or 25).
		// - 22050 Hz / 2 channels: EA SCHl PT\0\0 audio defaults when tags 0x84 / 0x82 are absent
		//   (all 5 retail TPW/TPI movies specify 0x82=2 and omit 0x84, confirmed 22050 Hz stereo via ffprobe).
		int frameRate = 15;
		int sampleRate = 22050;
		int channels = 2;
		int width = 0;
		int height = 0;

		var frameChunks = new List<(int Offset, int Length)>();
		var audio = new List<short>();

		int pos = 0;
		while ( pos + 8 <= data.Length )
		{
			uint tag = BinaryPrimitives.ReadUInt32LittleEndian( data.AsSpan( pos, 4 ) );
			int size = BinaryPrimitives.ReadInt32LittleEndian( data.AsSpan( pos + 4, 4 ) );
			if ( size < 8 || pos + size > data.Length )
				throw new InvalidDataException( $"Invalid EA chunk size {size} at offset {pos}." );

			int payloadOffset = pos + 8;
			int payloadLength = size - 8;
			var payload = data.AsSpan( payloadOffset, payloadLength );
			switch ( tag )
			{
				case TagSchl:
					ParsePtHeader( payload, ref frameRate, ref sampleRate, ref channels );
					break;
				case TagPiqt:
					if ( payloadLength < 8 )
						throw new InvalidDataException( $"Truncated pIQT frame header at offset {pos}." );
					if ( width == 0 )
					{
						width = BinaryPrimitives.ReadUInt16LittleEndian( payload );
						height = BinaryPrimitives.ReadUInt16LittleEndian( payload.Slice( 2, 2 ) );
					}
					frameChunks.Add( (payloadOffset, payloadLength) );
					break;
				case TagScdl:
					EaAdpcmDecoder.DecodeChunk( payload, channels, audio );
					break;
				case TagSccl:
					break;
				case TagScel:
					pos = data.Length;
					continue;
				default:
					string tagName = Encoding.ASCII.GetString( data, pos, 4 );
					Log?.Warning( $"TgqMovieFile: skipping unknown EA chunk '{tagName}' (0x{tag:X8}, {size} bytes) at offset {pos}." );
					break;
			}

			pos += size;
		}

		if ( frameChunks.Count == 0 )
			throw new InvalidDataException( "No pIQT video frames found in TGQ stream." );

		return new TgqMovieFile( data, frameChunks.ToArray(), width, height, frameRate, sampleRate, channels, audio.ToArray() );
	}

	private static void ParsePtHeader( ReadOnlySpan<byte> payload, ref int frameRate, ref int sampleRate, ref int channels )
	{
		if ( payload.Length < 4 || payload[0] != (byte)'P' || payload[1] != (byte)'T' )
			return;

		int i = 4;
		while ( i < payload.Length )
		{
			byte key = payload[i++];
			if ( key == 0xFF )
				break;

			// In EA PT\0\0 headers, 0xFD introduces a nested parameter sub-group whose inner entries
			// use the same 1-byte tag + 1-byte length + N-byte big-endian value layout (e.g. fd 85 03 ...).
			if ( key == 0xFD )
				continue;

			if ( i >= payload.Length )
				break;

			byte len = payload[i++];
			if ( i + len > payload.Length )
				break;

			var val = payload.Slice( i, len );
			switch ( key )
			{
				case 0x1B:
					int fps = ReadBigEndianInt( val );
					if ( fps > 0 ) frameRate = fps;
					break;
				case 0x82:
					int ch = ReadBigEndianInt( val );
					if ( ch > 0 ) channels = ch;
					break;
				case 0x84:
					int sr = ReadBigEndianInt( val );
					if ( sr > 0 ) sampleRate = sr;
					break;
			}

			i += len;
		}
	}

	private static int ReadBigEndianInt( ReadOnlySpan<byte> bytes )
	{
		int value = 0;
		for ( int i = 0; i < bytes.Length; i++ )
			value = (value << 8) | bytes[i];
		return value;
	}
}
