using NAudio.Wave;
using System.Collections.Concurrent;
using System.Globalization;

namespace OpenTPW;

/// <summary>Owns music playback on the game thread; device callbacks only queue completions.</summary>
internal sealed class MusicPlayer : IDisposable
{
	private readonly BaseFileSystem files;
	private readonly Func<IWavePlayer> createOutput;
	private readonly Func<Stream, WaveStream> decode;
	private readonly Random random;
	private readonly ConcurrentQueue<(IWavePlayer Output, Exception? Error)> completions = new();
	private MusicSource? source;
	private IWavePlayer? output;
	private WaveStream? reader;
	private Stream? compressed;
	private string? context;
	private bool disposed;
	private float volume = .6f;
	public float Volume
	{
		get => volume;
		set
		{
			volume = float.IsFinite( value ) ? Math.Clamp( value, 0, 1 ) : 0;
			if ( output != null ) output.Volume = volume;
		}
	}
	public string? CurrentTrack { get; private set; }

	public MusicPlayer( BaseFileSystem files, Func<IWavePlayer>? createOutput = null,
		Func<Stream, WaveStream>? decode = null, Random? random = null )
	{
		this.files = files;
		this.createOutput = createOutput ?? (() => new WaveOutEvent());
		this.decode = decode ?? (stream => new StreamMediaFoundationReader( stream ));
		this.random = random ?? Random.Shared;
		try
		{
			using var stream = files.OpenRead( "sound.sam" );
			var settings = new SettingsFile( stream );
			if ( float.TryParse( settings["DefaultVolume.MUSIC"], CultureInfo.InvariantCulture, out var percent ) )
				Volume = percent / 100;
			if ( settings["SoundInfo.SOUNDON"] == "0" ) Volume = 0;
		}
		catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException )
		{
			Log?.Warning( "Unable to read music settings: " + ex.Message );
		}
	}

	/// <summary>A null theme selects player-screen music. Park loading is silent.</summary>
	public void Update( string? theme, bool park = false, bool loading = false )
	{
		if ( disposed ) return;
		string next = loading ? "loading" : park ? $"park/{theme}" : theme == null ? "players" : $"lobby/{theme}";
		if ( context != next )
		{
			StopTrack();
			source?.Dispose();
			source = null;
			context = next;
			if ( !loading )
			{
				try
				{
					string root = theme == null ? "global" : $"levels/{theme}";
					string path = theme == null ? "global/sound/cat_globallobbysfx" :
						park ? $"{root}/Music/cat_music" : $"{root}/Sound/cat_locallobbymusic";
					// tp_nocd.exe: 005E1B65 (local lobby 1), 0051E741 (park 2).
					source = new MusicSource( files, root, path, theme == null || park ? 2 : 1 );
					StartTrack();
				}
				catch ( Exception ex ) { DisableContext( ex ); }
			}
		}
		while ( completions.TryDequeue( out var completion ) )
		{
			if ( !ReferenceEquals( completion.Output, output ) ) continue;
			if ( completion.Error != null ) { DisableContext( completion.Error ); continue; }
			StopTrack();
			try { StartTrack(); }
			catch ( Exception ex ) { DisableContext( ex ); }
		}
	}

	private void StartTrack()
	{
		if ( source == null ) return;
		var sound = source.Read( source.ChooseSample( random ) );
		if ( sound.SoundType is not (MP2File.SoundTypes.MP2_MONO or MP2File.SoundTypes.MP2_STEREO) )
			throw new InvalidDataException( "Unsupported music encoding: " + sound.SoundType );
		compressed = new MemoryStream( sound.SoundData, writable: false );
		reader = decode( compressed );
		output = createOutput();
		var playingOutput = output;
		output.PlaybackStopped += (_, args) => completions.Enqueue( (playingOutput, args.Exception) );
		output.Init( reader );
		output.Volume = Volume;
		output.Play();
		CurrentTrack = sound.Name;
	}

	private void StopTrack()
	{
		var stopped = output;
		var stoppedReader = reader;
		var stoppedStream = compressed;
		output = null;
		reader = null;
		compressed = null;
		CurrentTrack = null;
		// A removed device can also fail during Stop/Dispose. Still release
		// the decoder and stream, and keep audio errors out of the game loop.
		Release( () => stopped?.Stop() );
		Release( () => stopped?.Dispose() );
		Release( () => stoppedReader?.Dispose() );
		Release( () => stoppedStream?.Dispose() );
	}

	private static void Release( Action release )
	{
		try { release(); }
		catch ( Exception ex )
		{
			Log?.Warning( "Unable to release music output: " + ex.Message );
		}
	}

	private void DisableContext( Exception error )
	{
		StopTrack();
		source?.Dispose();
		source = null;
		// Do not retry a failed device/asset every frame. A scene change retries it.
		Log?.Warning( $"Music unavailable in {context}: {error.Message}" );
	}

	public void Dispose()
	{
		if ( disposed ) return;
		disposed = true;
		StopTrack();
		source?.Dispose();
		source = null;
	}
}
