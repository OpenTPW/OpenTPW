namespace OpenTPW.Audio;

/// <summary>One output clock with independently stoppable voices. No per-sound-ID suppression.</summary>
public class SoundMixer
{
	/// <summary>A logical voice retains its position even while the output budget makes it inaudible.</summary>
	private sealed class Voice
	{
		internal float[] Samples = [];
		internal int Position;
		internal float Volume;
		internal bool Loop;
		internal long Delay;
		internal uint Priority;
		// Waiting for its PCM: counts as playing, but neither mixes nor ends.
		internal bool Pending;
	}
	private Exception? failure;
	private readonly object sync = new();
	private readonly Dictionary<long, Voice> voices = new();
	private long nextHandle;
	private readonly MixerPolicy policy;
	private int voiceLimit;
	/// <summary>Current audible budget, bounded by the host policy rather than a game-specific constant.</summary>
	public int VoiceLimit
	{
		get { lock ( sync ) return voiceLimit; }
		set { if ( value < 1 || value > policy.MaximumVoices ) throw new ArgumentOutOfRangeException( nameof(value) ); lock ( sync ) voiceLimit = value; }
	}
	public int SampleRate { get; }
	public int Channels { get; }
	public int ActiveVoices { get { lock ( sync ) return voices.Count; } }

	public SoundMixer( MixerPolicy policy, int sampleRate = 44100, int channels = 2 )
	{
		ArgumentNullException.ThrowIfNull( policy );
		new PcmFormat( sampleRate, channels ).Validate();
		if ( policy.MaximumVoices < 1 ) throw new ArgumentOutOfRangeException( nameof(policy) );
		this.policy = policy; voiceLimit = policy.MaximumVoices;
		SampleRate = sampleRate; Channels = channels;
	}
	/// <summary>
	/// Queues interleaved PCM and returns a non-zero handle. Higher <paramref name="priority"/> wins when voices exceed
	/// <see cref="VoiceLimit"/>; lower-ranked voices are silent but keep advancing.
	/// </summary>
	public long Play( float[] samples, float volume = 1, bool loop = false, int delayMilliseconds = 0, uint priority = 0xffff )
	{
		if ( samples.Length == 0 || samples.Length % Channels != 0 ) throw new ArgumentException( "Audio must contain complete frames", nameof(samples) );
		if ( !float.IsFinite( volume ) || volume < 0 || volume > 1 || delayMilliseconds < 0 ) throw new ArgumentOutOfRangeException( nameof(volume) );
		lock ( sync )
		{
			long handle = checked(++nextHandle);
			voices.Add( handle, new Voice { Samples = samples, Volume = volume, Loop = loop,
				Delay = (long)delayMilliseconds * SampleRate / 1000 * Channels, Priority = priority } );
			return handle;
		}
	}
	/// <summary>
	/// Starts a voice whose PCM is still being decoded. It is playing from now on and starts sounding
	/// (after its delay) once the data arrives; a decode failure is reported by <see cref="ThrowIfFailed"/>.
	/// </summary>
	public long Play( Task<float[]> samples, float volume = 1, bool loop = false, int delayMilliseconds = 0, uint priority = 0xffff )
	{
		if ( samples.IsCompletedSuccessfully ) return Play( samples.Result, volume, loop, delayMilliseconds, priority );
		if ( !float.IsFinite( volume ) || volume < 0 || volume > 1 || delayMilliseconds < 0 ) throw new ArgumentOutOfRangeException( nameof(volume) );
		long handle;
		lock ( sync )
		{
			handle = checked(++nextHandle);
			voices.Add( handle, new Voice { Volume = volume, Loop = loop, Pending = true,
				Delay = (long)delayMilliseconds * SampleRate / 1000 * Channels, Priority = priority } );
		}
		samples.ContinueWith( task =>
		{
			lock ( sync )
			{
				if ( !voices.TryGetValue( handle, out var voice ) ) return;
				if ( task.IsCompletedSuccessfully && task.Result.Length != 0 && task.Result.Length % Channels == 0 )
				{
					voice.Samples = task.Result; voice.Pending = false; return;
				}
				voices.Remove( handle );
				failure ??= task.Exception?.InnerException ?? new InvalidDataException( "Decoded audio has no complete frames" );
			}
		}, TaskScheduler.Default );
		return handle;
	}
	/// <summary>Rethrows the first failed background decode on the caller's thread.</summary>
	public void ThrowIfFailed()
	{
		Exception? error;
		lock ( sync ) { error = failure; failure = null; }
		if ( error != null ) throw new InvalidDataException( "Sound decoding failed", error );
	}
	public bool IsPlaying( long handle ) { lock ( sync ) return voices.ContainsKey( handle ); }
	/// <summary>True while a voice started with a decoding task still waits for its PCM.</summary>
	public bool IsPending( long handle ) { lock ( sync ) return voices.TryGetValue( handle, out var voice ) && voice.Pending; }
	public void SetVolume( long handle, float volume )
	{
		if ( !float.IsFinite( volume ) || volume < 0 || volume > 1 ) throw new ArgumentOutOfRangeException( nameof(volume) );
		lock ( sync ) if ( voices.TryGetValue( handle, out var voice ) ) voice.Volume = volume;
	}
	public void Stop( long handle ) { lock ( sync ) voices.Remove( handle ); }
	public void StopAll() { lock ( sync ) voices.Clear(); }
	public double PositionSeconds( long handle )
	{
		lock ( sync ) return voices.TryGetValue( handle, out var voice ) ? (double)voice.Position / SampleRate / Channels : 0;
	}
	/// <summary>Writes count interleaved float samples, including silence, into a reusable output block.</summary>
	public int Read( float[] buffer, int offset, int count )
	{
		if ( count % Channels != 0 ) throw new ArgumentException( "Audio read must contain complete frames", nameof(count) );
		ArgumentNullException.ThrowIfNull( buffer );
		if ( offset < 0 || count < 0 || offset > buffer.Length - count ) throw new ArgumentOutOfRangeException( nameof(count) );
		for ( int i = offset; i < offset + count; ++i ) buffer[i] = 0;
		lock ( sync )
		{
			// Audio_RankOutputCandidates (US 0x6b62b0)/Audio_RankOutputCandidatesInternal (US 0x6b6bc0) rank output candidates without destroying logical instances.
			// Audio_InsertInstance (US 0x6b6a80) inserts new instances at the head, so newer equal-priority instances are visited first.
			var ranked = voices.OrderByDescending( entry => entry.Value.Priority );
			var ordered = (policy.NewestFirst ? ranked.ThenByDescending( entry => entry.Key ) : ranked.ThenBy( entry => entry.Key )).ToArray();
			for ( int i = 0; i < count; i += Channels )
			{
				int audible = 0;
				foreach ( var (_, voice) in ordered )
				{
					if ( voice.Pending ) continue;
					if ( voice.Delay > 0 ) { voice.Delay -= Channels; continue; }
					if ( voice.Position == voice.Samples.Length )
					{
						if ( !voice.Loop ) continue;
						voice.Position = 0;
					}
					bool mix = audible++ < voiceLimit;
					if ( !mix && !policy.AdvanceVirtualVoices ) continue;
					for ( int channel = 0; channel < Channels; ++channel )
					{
						float sample = voice.Samples[voice.Position++];
						if ( mix ) buffer[offset + i + channel] += sample * voice.Volume;
					}
				}
			}
			foreach ( var (handle, voice) in ordered )
				if ( !voice.Pending && !voice.Loop && voice.Position == voice.Samples.Length ) voices.Remove( handle );
		}
		for ( int i = offset; i < offset + count; ++i ) buffer[i] = Math.Clamp( buffer[i], -1, 1 );
		return count;
	}
}
