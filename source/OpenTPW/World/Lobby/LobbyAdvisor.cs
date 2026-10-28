namespace OpenTPW;

/// <summary>The lobby response queue (0x5d1180), global speech and gesture playback.</summary>
public sealed class LobbyAdvisor
{
	// Response rows at 0x764a38: response ID, speech ID, lip ID, default gesture selector -1.
	private sealed record ResponseEntry( uint Speech, uint Lip, int CueMilliseconds = 0 );
	private static readonly Dictionary<int, ResponseEntry> Responses = new()
	{
		[0x186] = new( 465, 465 ), [0x187] = new( 466, 466 ), [0x18e] = new( 471, 471 ),
		// Selector 28 (0x769a58) contains a 17000 ms cue and no gestures; 0x5950a0 falls back to the default sequence.
		[0x189] = new( 468, 468, 17000 ), [0x18a] = new( 469, 469 ), [0x23a] = new( 588, 588 ),
		[0x250] = new( 611, 611 ), [0x251] = new( 612, 612 ), [0x252] = new( 613, 613 ),
		[0x253] = new( 614, 614 ), [0x254] = new( 615, 615 ), [0x255] = new( 616, 616 ),
		[0x256] = new( 617, 617 ), [0x257] = new( 638, 0 ), [0x258] = new( 619, 619 )
	};
	private readonly AudioSystem audio;
	private readonly Func<int> nextRandom;
	private readonly Queue<int> queue = new();
	/// <summary>A response's speech sample, its length in milliseconds and its lip boundary data, resolved once.</summary>
	private sealed record Prepared( uint Sample, int Duration, byte[] Lips );
	private readonly Dictionary<int, Prepared> prepared = new();
	private readonly ModelAnimation[] animations;
	private readonly int[] durations;
	private LobbyAdvisorSequence? sequence;
	private LobbyAdvisorLips? lips;
	private int segment;
	private int segmentStart;
	private double started;
	private long voice;
	private uint sample;
	private bool voiceStarted;
	private bool cueSent;
	private bool enabled = true;
	public bool Enabled
	{
		get => enabled;
		set { enabled = value; if ( !value ) Cancel(); }
	}
	public event Action<int>? ResponseCue;
	public LobbyAdvisorGeometry Geometry { get; } = new();
	public bool Active => sequence != null;
	public int? Response { get; private set; }
	public int Mouth => lips?.Mouth ?? 1;
	public ModelAnimation Animation => animations[sequence!.Segments[segment]];
	public float Frame { get; private set; }
	public int PendingCount => queue.Count;

	public LobbyAdvisor( AudioSystem audio, Func<int>? nextRandom = null )
	{
		this.audio = audio;
		this.nextRandom = nextRandom ?? RetailRandom.Next;
		animations = AnimationFiles.Segments( LobbyAdvisorGeometry.ModelPath, AnimationSlot.M ).Select( path => new ModelAnimation( path ) ).ToArray();
		if ( animations.Length != 15 ) throw new InvalidDataException( "Advisor requires fifteen M segments" );
		durations = animations.Select( animation => (int)(animation.Length * 1000 / ModelAnimation.FramesPerSecond) ).ToArray();
	}

	public void Welcome( bool hasPlayers )
	{
		Say( hasPlayers ? 0x18e : 0x186, true );
		if ( !hasPlayers ) Say( 0x187 );
	}
	public void Say( int response, bool replace = false )
	{
		if ( !Responses.ContainsKey( response ) ) throw new ArgumentOutOfRangeException( nameof(response), $"Unimplemented advisor response {response:X}" );
		// 0x595500 rejects playback while the player's AdvisorOn byte is clear.
		if ( !Enabled ) return;
		if ( !prepared.ContainsKey( response ) )
		{
			var row = Responses[response];
			uint id = row.Speech;
			var map = audio.Assets.Category( "global/speech", "cat_speech" );
			if ( !map.Sounds.TryGetValue( id, out var sound ) )
				throw new InvalidDataException( $"Advisor response {response:X} references missing speech {id}" );
			if ( sound.Definitions.Count != 1 || sound.Definitions[0].Samples.Count != 1 )
				throw new InvalidDataException( "Advisor response requires a unique speech sample" );
			var entry = sound.Definitions[0].Samples[0];
			// Decode in the background now; playback starts after the response delay.
			_ = audio.Assets.PcmAsync( audio.Assets.Sample( "global/speech", map, entry ) );
			// 0x595500 permits an absent LIP resource; row 0x257 explicitly uses lip ID zero.
			byte[] boundaries = row.Lip == 0 ? BitConverter.GetBytes( -1 ) : FileSystem.ReadAllBytes( $"global/speech/lips/sp_{row.Lip:000}.lip" );
			_ = new LobbyAdvisorLips( boundaries );
			prepared.Add( response, new( id, checked((int)entry.DurationMilliseconds), boundaries ) );
		}
		if ( replace ) Cancel();
		queue.Enqueue( response );
	}
	public void Cancel()
	{
		queue.Clear();
		audio.Stop( voice );
		voice = 0;
		sequence = null;
		Response = null;
		lips = null;
	}

	public void Update( double seconds )
	{
		if ( sequence == null )
		{
			if ( !queue.TryDequeue( out int response ) ) return;
			// Resource loading takes place at enqueue time, before the playback clock starts.
			var entry = prepared[response];
			sample = entry.Sample;
			lips = new( entry.Lips );
			sequence = LobbyAdvisorSequence.Create( entry.Duration, durations, nextRandom );
			Response = response;
			started = seconds;
			segment = segmentStart = 0;
			voiceStarted = false;
			cueSent = false;
		}
		int elapsed = Math.Max( 0, (int)((seconds - started) * 1000) );
		// 0x595500 starts at now-200, then offsets both speech and lip clock by 1000.
		if ( !voiceStarted && elapsed >= 800 )
		{
			voice = audio.Play( "global/speech", "cat_speech", sample );
			voiceStarted = true;
		}
		lips!.Update( elapsed - 800, nextRandom );
		int cue = Responses[Response!.Value].CueMilliseconds;
		if ( !cueSent && cue != 0 && elapsed >= cue + 800 )
		{
			cueSent = true;
			// 0x595d30 consumes the timed marker once, independently of gesture completion.
			ResponseCue?.Invoke( Response.Value );
			if ( sequence == null ) return;
		}
		// Animation completion advances one segment per game update (0x595e87).
		int duration = durations[sequence.Segments[segment]];
		if ( elapsed - segmentStart >= duration && segment + 1 < sequence.Segments.Length )
		{
			segment++;
			segmentStart = elapsed;
		}
		Frame = Animation.StartFrame + Math.Min( Animation.Length, (elapsed - segmentStart) * ModelAnimation.FramesPerSecond / 1000 );
		if ( elapsed >= sequence.DurationMilliseconds + 800 && !audio.IsPlaying( voice ) )
		{
			sequence = null;
			Response = null;
			lips = null;
		}
	}
}
