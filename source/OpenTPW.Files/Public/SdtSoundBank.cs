using System.Text;

namespace OpenTPW;

/// <summary>Reads individual sounds without keeping an entire music bank in memory.</summary>
public sealed class SdtSoundBank : IDisposable
{
	private readonly Stream stream;
	private readonly uint[] offsets;
	public int Count => offsets.Length;

	/// <summary>Takes ownership of the seekable stream, including on a failed load.</summary>
	public SdtSoundBank( Stream stream )
	{
		this.stream = stream;
		try
		{
			using var reader = new BinaryReader( stream, Encoding.ASCII, leaveOpen: true );
			uint count = reader.ReadUInt32();
			if ( count > (stream.Length - 4) / 4 || count > 65535 )
				throw new InvalidDataException( "Invalid SDT offset table." );
			offsets = new uint[count];
			for ( int i = 0; i < offsets.Length; i++ )
			{
				offsets[i] = reader.ReadUInt32();
				if ( offsets[i] < 4 + count * 4 || offsets[i] > stream.Length - 40 )
					throw new InvalidDataException( "SDT sound offset is outside the bank." );
			}
		}
		catch
		{
			stream.Dispose();
			throw;
		}
	}

	/// <summary>Category sample numbers are one-based SDT indices.</summary>
	public MP2File ReadSound( int sample )
	{
		if ( sample < 1 || sample > Count )
			throw new InvalidDataException( "Music category refers to a missing SDT sample." );
		return SoundFile.ReadEntry( stream, offsets[sample - 1], dataOnly: true );
	}

	public void Dispose() => stream.Dispose();
}
