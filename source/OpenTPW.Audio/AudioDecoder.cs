using SDL;
using static SDL.SDL3;

namespace OpenTPW.Audio;

/// <summary>Decodes SDT payloads and converts once to the host PCM format, before caching or playback.</summary>
public static unsafe class AudioDecoder
{
	public static float[] Decode( MP2File file, PcmFormat target )
	{
		ArgumentNullException.ThrowIfNull( file ); target.Validate();
		if ( file.SoundType == MP2File.SoundTypes.NONE ) return new float[target.Channels];
		if ( file.SoundData.Length == 0 || file.SampleRate <= 0 ) throw new InvalidDataException( $"Invalid sound data: {file.Name}" );
		if ( file.SoundType == MP2File.SoundTypes.WAV )
		{
			// Retail packed 40-byte SDT type 2 contains mono S16LE directly (both games' data),
			// unlike standalone RIFF/WAVE input. Do not send raw PCM to SDL's RIFF parser.
			if ( file.Header == 40 && !file.SoundData.AsSpan().StartsWith( "RIFF"u8 ) )
			{
				if ( file.BitsPerSample != 16 || file.SoundData.Length % 2 != 0 ) throw new InvalidDataException( $"Invalid SDT PCM: {file.Name}" );
				var pcm = new SDL_AudioSpec { freq = file.SampleRate, channels = 1, format = SDL_AudioFormat.SDL_AUDIO_S16LE };
				fixed ( byte* source = file.SoundData ) return ConvertPcm( pcm, source, file.SoundData.Length, target, file.Name );
			}
			fixed ( byte* source = file.SoundData )
			{
				var io = SDL_IOFromConstMem( (IntPtr)source, (nuint)file.SoundData.Length );
				if ( io == null ) throw new InvalidDataException( $"WAV input failed: {SDL_GetError()}" );
				SDL_AudioSpec spec; byte* data = null; uint length = 0;
				if ( !SDL_LoadWAV_IO( io, true, &spec, &data, &length ) ) throw new InvalidDataException( $"WAV decode failed for {file.Name}: {SDL_GetError()}" );
				try { return ConvertPcm( spec, data, checked((int)length), target, file.Name ); }
				finally { SDL_free( data ); }
			}
		}
		if ( file.SoundType is not (MP2File.SoundTypes.MP2_MONO or MP2File.SoundTypes.MP2_STEREO) )
			throw new InvalidDataException( $"Unsupported sound encoding {file.SoundType}: {file.Name}" );
		// The verified 421-byte silent payload has three complete Layer I frames. Media Foundation
		// dropped it, but minimp3 does not need a duration fabricated from the SDT Samples field.
		var raw = MpegDecoder.Decode( file.SoundData );
		// All 8,591 MPEG entries in the two retail SDT sets end in one zero alignment byte.
		// Other incomplete or corrupt tails must not silently turn into a shorter successful clip.
		if ( raw.TrailingBytes != 0 && (raw.TrailingBytes != 1 || file.SoundData[^1] != 0) )
			throw new InvalidDataException( $"Incomplete MPEG tail in {file.Name}: {raw.TrailingBytes} bytes." );
		var input = new SDL_AudioSpec { freq = raw.Format.SampleRate, channels = raw.Format.Channels, format = SDL_AudioFormat.SDL_AUDIO_S16LE };
		fixed ( short* data = raw.Samples ) return ConvertPcm( input, (byte*)data, checked(raw.Samples.Length * sizeof(short)), target, file.Name );
	}

	private static float[] ConvertPcm( SDL_AudioSpec source, byte* data, int length, PcmFormat target, string name )
	{
		if ( source.freq <= 0 || source.channels is not (1 or 2) || length <= 0 )
			throw new InvalidDataException( $"Invalid PCM format or empty sound: {name}" );
		var destination = new SDL_AudioSpec { freq = target.SampleRate, channels = target.Channels, format = SDL_AUDIO_F32 };
		byte* converted = null; int convertedLength = 0;
		if ( !SDL_ConvertAudioSamples( &source, data, length, &destination, &converted, &convertedLength ) )
			throw new InvalidDataException( $"Sound conversion failed for {name}: {SDL_GetError()}" );
		try
		{
			if ( convertedLength <= 0 || convertedLength % (target.Channels * sizeof(float)) != 0 )
				throw new InvalidDataException( $"Converted sound has no complete frames: {name}" );
			return new ReadOnlySpan<float>( converted, convertedLength / sizeof(float) ).ToArray();
		}
		finally { SDL_free( converted ); }
	}
}
