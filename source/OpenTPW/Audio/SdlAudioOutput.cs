using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace OpenTPW;

/// <summary>Pull-driven SDL3 float PCM output; owns its stream, callback context and audio subsystem reference.</summary>
internal sealed unsafe class SdlAudioOutput : IDisposable
{
	private readonly SoundMixer mixer;
	private readonly float[] block = new float[2048];
	private SDL_AudioStream* stream;
	private GCHandle context;
	private Exception? failure;
	private bool initialized;
	private long submittedFrames;
	internal long SubmittedFrames => Interlocked.Read( ref submittedFrames );

	public SdlAudioOutput( SoundMixer mixer )
	{
		this.mixer = mixer;
		if ( !SDL_InitSubSystem( SDL_InitFlags.SDL_INIT_AUDIO ) ) throw Error();
		initialized = true;
		try
		{
			context = GCHandle.Alloc( this );
			var spec = new SDL_AudioSpec { freq = mixer.SampleRate, channels = mixer.Channels, format = SDL_AUDIO_F32 };
			stream = SDL_OpenAudioDeviceStream( SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &spec, &Fill, GCHandle.ToIntPtr( context ) );
			if ( stream == null || !SDL_ResumeAudioStreamDevice( stream ) ) throw Error();
		}
		catch { Dispose(); throw; }
	}

	[UnmanagedCallersOnly( CallConvs = new[] { typeof( CallConvCdecl ) } )]
	private static void Fill( IntPtr userdata, SDL_AudioStream* stream, int additional, int total )
	{
		var output = (SdlAudioOutput)GCHandle.FromIntPtr( userdata ).Target!;
		try { if ( output.failure == null ) output.Feed( stream, additional ); }
		// Managed exceptions must never unwind through SDL's native audio thread.
		catch ( Exception error ) { Interlocked.CompareExchange( ref output.failure, error, null ); }
	}

	private void Feed( SDL_AudioStream* target, int bytes )
	{
		int frameBytes = checked(mixer.Channels * sizeof(float));
		int frames = Math.Max( 0, bytes / frameBytes + (bytes % frameBytes != 0 ? 1 : 0) );
		fixed ( float* data = block )
		{
			while ( frames > 0 )
			{
				int count = Math.Min( frames, block.Length / mixer.Channels ) * mixer.Channels;
				mixer.Read( block, 0, count );
				if ( !SDL_PutAudioStreamData( target, (IntPtr)data, count * sizeof(float) ) ) throw Error();
				Interlocked.Add( ref submittedFrames, count / mixer.Channels );
				frames -= count / mixer.Channels;
			}
		}
	}

	public void ThrowIfFailed()
	{
		if ( Volatile.Read( ref failure ) is { } error ) throw new InvalidOperationException( "SDL3 audio output failed", error );
	}

	public void Dispose()
	{
		if ( stream != null )
		{
			// Removing the callback acquires the stream lock and waits for an in-flight callback.
			SDL_SetAudioStreamGetCallback( stream, null, IntPtr.Zero );
			SDL_DestroyAudioStream( stream );
			stream = null;
		}
		if ( context.IsAllocated ) context.Free();
		if ( initialized ) { SDL_QuitSubSystem( SDL_InitFlags.SDL_INIT_AUDIO ); initialized = false; }
	}

	private static InvalidOperationException Error() => new( $"SDL3 audio: {SDL_GetError()}" );
}
