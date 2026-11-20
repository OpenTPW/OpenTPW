using SoundChannelPolicy = OpenTPW.Audio.SoundChannelPolicy;

namespace OpenTPW;

/// <summary>TPW policy from SoundChannelPolicy: 30 candidates, newest equal-priority voice first, virtual voices keep advancing.</summary>
public sealed class SoundMixer( int sampleRate = 44100, int channels = 2 )
	: Audio.SoundMixer( new Audio.MixerPolicy( SoundChannelPolicy.MaximumChannels ), sampleRate, channels );
