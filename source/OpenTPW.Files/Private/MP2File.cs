namespace OpenTPW;

public sealed class MP2File : ArchiveFile
{
	public enum SoundTypes
	{
		NONE = 0, //  on blanks
		WAV = 2, // on wav
		WAV_OLD = 3, // used before 1.7, like in the german cd version
		MP2_MONO = 36, // on mp2 (64kbit/s mono)
		MP2_STEREO = 37 // on mp2 (112kbit/s stereo)
	}

	public string Name { get; set; }
	public int Header { get; set; }
	public byte[] SoundData { get; set; }
	public byte[] Data { get; set; }
	public int SampleRate { get; set; }
	public int BitsPerSample { get; set; }

	public SoundTypes SoundType { get; set; }
	public int Samples { get; set; }

	public MP2File( int header, string name, byte[] soundData, int sampleRate, int bitsPerSample, int soundType, int samples, byte[] data )
	{
		Header = header;
		Name = name;
		SoundData = soundData;
		SampleRate = 22050;
		BitsPerSample = 16;
		SoundType = (SoundTypes)soundType;
		Samples = samples;
		Data = data;
	}

	public override byte[] GetData()
	{
		return Data;
	}
}
