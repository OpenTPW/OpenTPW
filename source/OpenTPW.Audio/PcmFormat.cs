namespace OpenTPW.Audio;

/// <summary>Interleaved output PCM format; hosts select rate and channel count explicitly.</summary>
public readonly record struct PcmFormat( int SampleRate, int Channels )
{
	internal void Validate()
	{
		if ( SampleRate <= 0 || Channels is not (1 or 2) ) throw new ArgumentOutOfRangeException( nameof(PcmFormat) );
	}
}

/// <summary>Host policy for audible candidates; logical voices are not limited by this budget.</summary>
public sealed record MixerPolicy( int MaximumVoices, bool NewestFirst = true, bool AdvanceVirtualVoices = true );

/// <summary>Original-rate MPEG PCM and byte accounting, independent of resampling.</summary>
public sealed record MpegPcm( short[] Samples, PcmFormat Format, int Layer, int ConsumedBytes, int TrailingBytes );
