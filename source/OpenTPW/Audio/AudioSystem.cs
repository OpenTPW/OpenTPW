using NAudio.Wave;

namespace OpenTPW;

/// <summary>Shared audio output for scene sounds, music and speech.</summary>
public sealed class AudioSystem : IDisposable
{
	public SoundMixer Mixer { get; } = new();
	public SoundAssets Assets { get; } = new();
	private readonly SoundSelection random;
	/// <summary>A flag-4 sound that picks its next sample when the current voice ends (0x6bdcd0).</summary>
	private sealed record Repeating( string Directory, string Category, SoundCategoryMap Map, SoundCategoryMap.Sound Sound,
		SoundCategoryMap.Definition Definition, int SampleIndex, long Voice );
	private readonly Dictionary<long, Repeating> repeating = new();
	/// <summary>The voice's own random gain, kept so channel volume changes can be reapplied.</summary>
	private sealed record ChannelGain( string Category, float Gain );
	private readonly Dictionary<long, ChannelGain> gains = new();
	private readonly SettingsFile settings = new( "sound.sam" );
	private readonly WaveOutEvent? output;
	private bool disposed;
	public bool Available { get; }
	public string? Failure { get; }
	private readonly float[] channelVolumes = new float[4];
	private byte[] currentPreferences = new byte[39];
	/// <summary>Snapshot the live options for modal preview rollback, including disabled channels' stored volumes.</summary>
	public byte[] SnapshotPreferences() => (byte[])currentPreferences.Clone();

	public AudioSystem( uint seed, bool openDevice = true )
	{
		random = new( seed );
		ConfigurePreferences( PlayerProfileFile.DefaultPreferences( settings ) );
		if ( !int.TryParse( settings["SoundInfo.DEFAULTQUALITY"], out int quality ) ) throw new InvalidDataException( "Invalid default sound quality" );
		ConfigureQuality( quality );
		if ( !openDevice ) { Available = true; return; }
		try
		{
			output = new WaveOutEvent();
			output.Init( Mixer );
			output.Play();
			Available = true;
		}
		catch ( Exception error ) when ( error is NAudio.MmException or PlatformNotSupportedException or System.Runtime.InteropServices.COMException )
		{
			output?.Dispose();
			output = null;
			Failure = error.Message;
		}
	}

	/// <summary>Plays a category sound (0x5187a0). Returns 0 when output is unavailable or the draw selects no sample.</summary>
	public long Play( string directory, string category, uint id )
	{
		ObjectDisposedException.ThrowIf( disposed, this );
		if ( !Available || id == 0 ) return 0;
		var map = Assets.Category( directory, category );
		if ( !map.Sounds.TryGetValue( id, out var sound ) ) throw new InvalidDataException( $"Sound {category}/{id} is not defined" );
		var definition = random.ChooseDefinition( sound );
		if ( definition == null ) return 0;
		var sample = random.ChooseSample( definition );
		if ( sample == null || sample.Bank == 0 ) return 0;
		var asset = Assets.Sample( directory, map, sample );
		// 0x6ba400 collapses a one-definition, one-sample repeat with no links/delay into a direct loop.
		bool directLoop = (sound.Flags & 8) != 0 || ((sound.Flags & 6) == 6 && sound.Definitions.Count == 1 &&
			definition.Samples.Count == 1 && definition.Relations.Count == 0 && definition.DelayMin == 0 && definition.DelayMax == 0);
		float gain = random.Range( definition.VolumeMin, definition.VolumeMax ) / 100f;
		// 0x6b5b00 scores a voice as (priority << 16) plus a distance weight; lobby sounds have no position, so 0xffff.
		long handle = Mixer.Play( Assets.PcmAsync( asset ), gain * ChannelVolume( category ),
			directLoop, random.Range( definition.DelayMin, definition.DelayMax ), unchecked((sound.Priority << 16) + 0xffff) );
		gains.Add( handle, new( category, gain ) );
		if ( !directLoop && (sound.Flags & 4) != 0 ) repeating.Add( handle, new( directory, category, map, sound, definition, definition.Samples.ToList().IndexOf( sample ), handle ) );
		return handle;
	}

	public bool IsPlaying( long handle ) => repeating.ContainsKey( handle ) || Mixer.IsPlaying( handle );
	public void Stop( long handle )
	{
		long voice = repeating.Remove( handle, out var entry ) ? entry.Voice : handle;
		Mixer.Stop( voice );
		gains.Remove( voice );
	}
	public void Update()
	{
		Mixer.ThrowIfFailed();
		foreach ( long handle in gains.Keys.ToArray() ) if ( !Mixer.IsPlaying( handle ) ) gains.Remove( handle );
		foreach ( var (handle, entry) in repeating.ToArray() )
		{
			if ( Mixer.IsPlaying( entry.Voice ) ) continue;
			var definition = entry.Definition;
			// 0x6bdcd0..0x6bddd7: eligible links select using the destination definition weights.
			var links = definition.Relations.Where( link => ((link.Parameters >> 16) & 255) == 0 ).ToArray();
			if ( links.Length != 0 )
			{
				uint total = checked((uint)links.Sum( link => (long)entry.Sound.Definitions[(int)link.Definition - 1].Weight ));
				if ( total == 0 ) throw new InvalidDataException( "Repeating sound links have zero weight" );
				uint draw = random.Next() % total, cumulative = 0;
				foreach ( var link in links )
				{
					var candidate = entry.Sound.Definitions[(int)link.Definition - 1];
					cumulative += candidate.Weight;
					if ( cumulative < draw ) continue;
					definition = candidate;
					break;
				}
			}
			// 0x6b6780 stores the last numeric index on the instance, even across definition changes.
			var sample = random.ChooseSample( definition, entry.SampleIndex, true );
			if ( sample == null || sample.Bank == 0 ) { repeating.Remove( handle ); continue; }
			float gain = random.Range( definition.VolumeMin, definition.VolumeMax ) / 100f;
			long voice = Mixer.Play( Assets.PcmAsync( Assets.Sample( entry.Directory, entry.Map, sample ) ), gain * ChannelVolume( entry.Category ),
				delayMilliseconds: random.Range( definition.DelayMin, definition.DelayMax ), priority: unchecked((entry.Sound.Priority << 16) + 0xffff) );
			gains.Add( voice, new( entry.Category, gain ) );
			repeating[handle] = entry with { Definition = definition,
				SampleIndex = definition.Samples.Count > 1 ? definition.Samples.ToList().IndexOf( sample ) : entry.SampleIndex, Voice = voice };
		}
	}
	public void ConfigurePreferences( byte[] preferences )
	{
		if ( preferences.Length != 39 ) throw new InvalidDataException( "Invalid audio preferences" );
		// Validate the complete block before changing any channel or the rollback snapshot.
		for ( int i = 0; i < 4; i++ )
			if ( BitConverter.ToUInt32( preferences, i * 8 + 4 ) > 100 ) throw new InvalidDataException( "Invalid audio channel volume" );
		currentPreferences = (byte[])preferences.Clone();
		for ( int i = 0; i < 4; i++ )
		{
			uint volume = BitConverter.ToUInt32( preferences, i * 8 + 4 );
			channelVolumes[i] = preferences[i * 8] == 0 ? 0 : volume / 100f;
		}
		// Category volume changes affect existing output too (0x4238c0 -> 0x518360/380/3a0).
		foreach ( var (handle, gain) in gains ) Mixer.SetVolume( handle, gain.Gain * ChannelVolume( gain.Category ) );
	}
	public void ConfigureQuality( int quality ) => Mixer.VoiceLimit = SoundChannelPolicy.Limit( settings, quality );
	private float ChannelVolume( string category ) => channelVolumes[category.EndsWith( "music", StringComparison.OrdinalIgnoreCase ) ? 1 :
		category.Equals( "cat_speech", StringComparison.OrdinalIgnoreCase ) ? 2 : 0];

	/// <summary>Plays a standalone sound file outside the category tables at <paramref name="volume"/>; channel volumes do not apply.</summary>
	public long PlayFile( string path, float volume = 1 )
	{
		ObjectDisposedException.ThrowIf( disposed, this );
		if ( !Available ) return 0;
		var file = new SoundFile( path );
		try { return Mixer.Play( Assets.Pcm( file.GetFile( new MemoryStream() ) ), volume ); }
		finally { file.Dispose(); }
	}
	public void Dispose()
	{
		if ( disposed ) return;
		disposed = true;
		repeating.Clear();
		gains.Clear();
		output?.Stop();
		output?.Dispose();
		Mixer.StopAll();
	}
}
