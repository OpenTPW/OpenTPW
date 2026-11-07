using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OpenTPW.Audio;

/// <summary>Layer I/II/III decoding through a per-clip minimp3 state and a bounded C ABI.</summary>
public static unsafe class MpegDecoder
{
	private const string Library = "OpenTPW.Audio.Native";
	// minimp3's MINIMP3_MAX_SAMPLES_PER_FRAME is 1152 frames times two channels.
	private const int MaximumSamples = 1152 * 2;
	[DllImport( Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "otp_mpeg_create" )]
	[DefaultDllImportSearchPaths( DllImportSearchPath.AssemblyDirectory )]
	private static extern DecoderHandle Create();
	[DllImport( Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "otp_mpeg_destroy" )]
	[DefaultDllImportSearchPaths( DllImportSearchPath.AssemblyDirectory )]
	private static extern void Destroy( IntPtr handle );
	[DllImport( Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "otp_mpeg_decode" )]
	[DefaultDllImportSearchPaths( DllImportSearchPath.AssemblyDirectory )]
	private static extern int DecodeFrame( DecoderHandle handle, byte* input, int length, short* output, int capacity,
		out int consumed, out int skipped, out int channels, out int rate, out int layer );

	/// <summary>Owns one native decoder; opaque state cannot be shared between concurrent clips.</summary>
	private sealed class DecoderHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public DecoderHandle() : base( true ) { }
		protected override bool ReleaseHandle() { Destroy( handle ); return true; }
	}

	public static MpegPcm Decode( ReadOnlySpan<byte> data )
	{
		if ( data.IsEmpty ) throw new InvalidDataException( "Empty MPEG stream." );
		using var decoder = Create();
		if ( decoder.IsInvalid ) throw new OutOfMemoryException( "Cannot allocate MPEG decoder." );
		var samples = new List<short>();
		var block = new short[MaximumSamples];
		int offset = 0, rate = 0, channels = 0, layer = 0;
		fixed ( byte* source = data ) fixed ( short* output = block )
			while ( offset < data.Length )
			{
				int frames = DecodeFrame( decoder, source + offset, data.Length - offset, output, block.Length,
					out int bytes, out int skipped, out int nextChannels, out int nextRate, out int nextLayer );
				if ( frames < 0 || bytes < 0 || bytes > data.Length - offset ) throw new InvalidDataException( "Invalid native MPEG result." );
				// Preserve incomplete tails as metadata; never fabricate samples or spin on zero progress.
				if ( frames == 0 ) break;
				if ( bytes == 0 || skipped != 0 || nextRate <= 0 || nextChannels is not (1 or 2) || frames * nextChannels > block.Length )
					throw new InvalidDataException( "Invalid MPEG frame or gap inside stream." );
				if ( rate != 0 && (rate != nextRate || channels != nextChannels || layer != nextLayer) )
					throw new InvalidDataException( "MPEG format changed inside a clip." );
				rate = nextRate; channels = nextChannels; layer = nextLayer;
				samples.AddRange( block.AsSpan( 0, frames * channels ) );
				offset += bytes;
			}
		if ( samples.Count == 0 ) throw new InvalidDataException( "MPEG stream has no complete audio frame." );
		return new( samples.ToArray(), new( rate, channels ), layer, offset, data.Length - offset );
	}
}
