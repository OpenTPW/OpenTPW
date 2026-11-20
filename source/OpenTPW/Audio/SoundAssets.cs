using NAudio.Wave;
using SDL;
using static SDL.SDL3;

namespace OpenTPW;

/// <summary>Lazy SDT/category loading, decoding and cached SDL3 PCM conversion. Paths are relative to the game data filesystem.</summary>
public sealed class SoundAssets
{
	private readonly Dictionary<string, SoundCategoryMap> categories = new( StringComparer.OrdinalIgnoreCase );
	private readonly Dictionary<string, SdtArchive> banks = new( StringComparer.OrdinalIgnoreCase );
	// Decoding runs on worker threads; each payload is decoded once however many plays wait for it.
	private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<float[]>> decoded = new( StringComparer.OrdinalIgnoreCase );
	private readonly System.Runtime.CompilerServices.ConditionalWeakTable<MP2File, Lazy<float[]>> decodedFiles = new();
	public SoundCategoryMap Category( string directory, string category )
	{
		string key = directory.TrimEnd( '/' ) + "/" + category;
		if ( !categories.TryGetValue( key, out var data ) )
			categories.Add( key, data = SoundCategoryMap.Parse( FileSystem.ReadAllBytes( key + "BANK.map" ), FileSystem.ReadAllBytes( key + "SFX.map" ) ) );
		return data;
	}
	public MP2File Sample( string directory, SoundCategoryMap map, SoundCategoryMap.Sample sample )
	{
		if ( sample.Bank == 0 || sample.Bank > map.Banks.Count ) throw new InvalidDataException( "Unbound sound sample" );
		string parent = directory.TrimEnd( '/' );
		parent = parent[..parent.LastIndexOf( '/' )];
		string path = parent + "/" + map.Banks[sample.Bank - 1].Path + "HD.sdt";
		if ( !banks.TryGetValue( path, out var bank ) ) banks.Add( path, bank = new SdtArchive( FileSystem.OpenRead( path ) ) );
		if ( sample.Id == 0 || sample.Id > bank.soundFiles.Count ) throw new InvalidDataException( $"Invalid sample {sample.Id} in {path}" );
		return bank.soundFiles[(int)sample.Id - 1];
	}
	/// <summary>
	/// Decoded PCM, shared by identical payloads. Cached bank entries hit the reference table;
	/// files read per call (<see cref="AudioSystem.PlayFile"/>) fall back to a content hash.
	/// </summary>
	public float[] Pcm( MP2File file ) => Entry( file ).Value;

	/// <summary>
	/// Decoded PCM without blocking the caller. A first decode takes 5-120 ms (music and speech the
	/// longest), which would otherwise stall the frame that starts the sound.
	/// </summary>
	public Task<float[]> PcmAsync( MP2File file )
	{
		var entry = Entry( file );
		return entry.IsValueCreated ? Task.FromResult( entry.Value ) : Task.Run( () => entry.Value );
	}

	private Lazy<float[]> Entry( MP2File file )
	{
		if ( decodedFiles.TryGetValue( file, out var known ) ) return known;
		string key = Convert.ToHexString( System.Security.Cryptography.SHA256.HashData( file.Data ) );
		var entry = decoded.GetOrAdd( key, _ => new Lazy<float[]>( () => Decode( file ), LazyThreadSafetyMode.ExecutionAndPublication ) );
		decodedFiles.AddOrUpdate( file, entry );
		return entry;
	}
	public static unsafe float[] Decode( MP2File file )
	{
		if ( file.SoundType == MP2File.SoundTypes.NONE ) return new float[2];
		if ( file.SoundData.Length == 0 || file.SampleRate <= 0 ) throw new InvalidDataException( $"Invalid sound data: {file.Name}" );
		// The shipped 421-byte silent MPEG clip has one valid frame and an incomplete tail.
		// Media Foundation drops it entirely; retain its declared PCM duration, only for this verified payload.
		if ( Convert.ToHexString( System.Security.Cryptography.SHA256.HashData( file.SoundData ) ) ==
			"8A8E99FAFAC281C339F64963E3E40535DB201AB6C84460C99A3EBB248EC21F21" )
			return new float[checked((int)((long)file.Samples / 2 * 44100 / file.SampleRate) * 2)];
		if ( file.SoundType == MP2File.SoundTypes.WAV )
		{
			fixed ( byte* source = file.SoundData )
			{
				var io = SDL_IOFromConstMem( (IntPtr)source, (nuint)file.SoundData.Length );
				if ( io == null ) throw new InvalidDataException( $"WAV input failed: {SDL_GetError()}" );
				SDL_AudioSpec spec;
				byte* data = null; uint length = 0;
				if ( !SDL_LoadWAV_IO( io, true, &spec, &data, &length ) ) throw new InvalidDataException( $"WAV decode failed for {file.Name}: {SDL_GetError()}" );
				try { return ConvertPcm( spec, data, checked((int)length), file.Name ); }
				finally { SDL_free( data ); }
			}
		}
		if ( file.SoundType is not (MP2File.SoundTypes.MP2_MONO or MP2File.SoundTypes.MP2_STEREO) )
			throw new InvalidDataException( $"Unsupported sound encoding {file.SoundType}: {file.Name}" );
		// The only NAudio use: Windows Media Foundation still decodes MPEG Layer II.
		// Replace this decoder for cross-platform playback; SDL3 owns all PCM conversion and output.
		using var bytes = new MemoryStream( file.SoundData, false );
		using var reader = new StreamMediaFoundationReader( bytes );
		var format = reader.WaveFormat;
		if ( format.Encoding != WaveFormatEncoding.Pcm || format.BitsPerSample != 16 )
			throw new InvalidDataException( $"Unexpected Media Foundation PCM format in {file.Name}: {format}" );
		using var pcm = new MemoryStream();
		reader.CopyTo( pcm );
		byte[] decoded = pcm.ToArray();
		var input = new SDL_AudioSpec { freq = format.SampleRate, channels = format.Channels, format = SDL_AudioFormat.SDL_AUDIO_S16LE };
		fixed ( byte* data = decoded ) return ConvertPcm( input, data, decoded.Length, file.Name );
	}

	/// <summary>Converts a complete decoded clip once, before the PCM cache, never at render time or per playback.</summary>
	private static unsafe float[] ConvertPcm( SDL_AudioSpec source, byte* data, int length, string name )
	{
		if ( source.freq <= 0 || source.channels is not (1 or 2) || length <= 0 )
			throw new InvalidDataException( $"Invalid PCM format or empty sound: {name}" );
		var destination = new SDL_AudioSpec { freq = 44100, channels = 2, format = SDL_AUDIO_F32 };
		byte* converted = null; int convertedLength = 0;
		if ( !SDL_ConvertAudioSamples( &source, data, length, &destination, &converted, &convertedLength ) )
			throw new InvalidDataException( $"Sound conversion failed for {name}: {SDL_GetError()}" );
		try
		{
			if ( convertedLength <= 0 || convertedLength % (2 * sizeof(float)) != 0 )
				throw new InvalidDataException( $"Converted sound has no complete stereo frames: {name}" );
			return new ReadOnlySpan<float>( converted, convertedLength / sizeof(float) ).ToArray();
		}
		finally { SDL_free( converted ); }
	}
}
