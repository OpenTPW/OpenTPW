using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace OpenTPW;

/// <summary>Lazy SDT/category loading and MPEG decoding. Paths are relative to the game data filesystem.</summary>
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
	public static float[] Decode( MP2File file )
	{
		if ( file.SoundType == MP2File.SoundTypes.NONE ) return new float[2];
		if ( file.SoundData.Length == 0 || file.SampleRate <= 0 ) throw new InvalidDataException( $"Invalid sound data: {file.Name}" );
		// The shipped 421-byte silent MPEG clip has one valid frame and an incomplete tail.
		// Media Foundation drops it entirely; retain its declared PCM duration, only for this verified payload.
		if ( Convert.ToHexString( System.Security.Cryptography.SHA256.HashData( file.SoundData ) ) ==
			"8A8E99FAFAC281C339F64963E3E40535DB201AB6C84460C99A3EBB248EC21F21" )
			return new float[checked((int)((long)file.Samples / 2 * 44100 / file.SampleRate) * 2)];
		using var bytes = new MemoryStream( file.SoundData, false );
		using WaveStream reader = file.SoundType == MP2File.SoundTypes.WAV ? new WaveFileReader( bytes ) :
			file.SoundType is MP2File.SoundTypes.MP2_MONO or MP2File.SoundTypes.MP2_STEREO ? new StreamMediaFoundationReader( bytes ) :
			throw new InvalidDataException( $"Unsupported sound encoding {file.SoundType}: {file.Name}" );
		ISampleProvider samples = reader.ToSampleProvider();
		if ( samples.WaveFormat.SampleRate != 44100 ) samples = new WdlResamplingSampleProvider( samples, 44100 );
		if ( samples.WaveFormat.Channels == 1 ) samples = new MonoToStereoSampleProvider( samples );
		if ( samples.WaveFormat.Channels != 2 ) throw new InvalidDataException( $"Unsupported channel count in {file.Name}" );
		var result = new List<float>();
		var block = new float[8192];
		for ( int count; (count = samples.Read( block, 0, block.Length )) > 0; ) result.AddRange( block.AsSpan( 0, count ) );
		if ( result.Count == 0 ) throw new InvalidDataException( $"Decoded sound is empty: {file.Name}" );
		return result.ToArray();
	}
}
