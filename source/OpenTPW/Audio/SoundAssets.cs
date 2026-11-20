namespace OpenTPW;

/// <summary>Lazy SDT/category loading, decoding and cached SDL3 PCM conversion. Paths are relative to the game data filesystem.</summary>
public sealed class SoundAssets
{
	private readonly Dictionary<string, SoundCategoryMap> categories = new( StringComparer.OrdinalIgnoreCase );
	private readonly Dictionary<string, SdtArchive> banks = new( StringComparer.OrdinalIgnoreCase );
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
	private readonly Audio.AudioClipCache pcm = new( new Audio.PcmFormat( 44100, 2 ) );
	public float[] Pcm( MP2File file ) => pcm.Get( file );
	public Task<float[]> PcmAsync( MP2File file ) => pcm.GetAsync( file );
	public static float[] Decode( MP2File file ) => Audio.AudioDecoder.Decode( file, new Audio.PcmFormat( 44100, 2 ) );
}
