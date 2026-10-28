namespace OpenTPW;

// BIG SHOUT TO Toksisitee - https://github.com/Toksisitee/PopSoundEditor

public class SoundFile : BaseFormat
{
	private ExpandedMemoryStream memoryStream;
	public byte[] buffer;

	public SoundFile( string path )
	{
		ReadFromFile( path );
	}

	public SoundFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	public void Dispose()
	{
		memoryStream.Dispose();
	}

	protected override void ReadFromStream( Stream stream )
	{
		// Set up read buffer
		var tempStreamReader = new StreamReader( stream );
		var fileLength = (int)tempStreamReader.BaseStream.Length;

		buffer = new byte[fileLength];
		tempStreamReader.BaseStream.Read( buffer, 0, fileLength );
		tempStreamReader.Close();

		memoryStream = new ExpandedMemoryStream( buffer );
	}

	public MP2File GetFile( MemoryStream stream, int offset = 0, bool dataOnly = false )
	{
		/*
		 * 4 bytes: Header size
		 * 4 bytes: Data size
		 * 16 bytes: File name (usually null terminated)
		 * 4 bytes: Sample rate (Int16)
		 * 4 bytes: BitsPerSample
		 * 4 bytes: Sound type
		 * 4 bytes: Unknown
		 * 4 bytes: Samples
		 * 4 bytes: Unknown
		 * n bytes: File data
		*/

		memoryStream.Seek( offset, SeekOrigin.Begin );

		var headerSize = memoryStream.ReadInt32();
		var soundDataSize = memoryStream.ReadInt32();
		var fileName = memoryStream.ReadString( 16 ).TrimEnd( '\0' );
		
		// Add MP2 to end of file so we can handle it properly
		if ( !fileName.EndsWith( ".mp2" ) )
		{
			bool hasExtension = fileName.Contains( '.' );
			if ( !hasExtension )
			{
				fileName += ".mp2";
			}
			else
			{
				fileName = fileName.Substring( 0, fileName.LastIndexOf( "." ) ) + ".mp2";
			}
		}

		// The 40-byte retail header packs rate, resolution and type into four bytes.
		// Reading resolution/type as Int32 shifts both the PCM size and MPEG payload.
		int sampleRate, bitsPerSample, soundType;
		if ( headerSize == 40 )
		{
			sampleRate = BitConverter.ToUInt16( memoryStream.ReadBytes( 2 ) );
			bitsPerSample = memoryStream.ReadByte();
			soundType = memoryStream.ReadByte();
		}
		else if ( headerSize >= 48 )
		{
			sampleRate = memoryStream.ReadInt32();
			bitsPerSample = memoryStream.ReadInt32();
			soundType = memoryStream.ReadInt32();
		}
		else throw new InvalidDataException( $"Unsupported SDT sound header size {headerSize}" );

		// Unknown
		_ = memoryStream.ReadInt32();

		var samples = memoryStream.ReadInt32();

		// Unknown
		_ = memoryStream.ReadInt32();

		// Rest of Data
		memoryStream.Seek( offset + headerSize, SeekOrigin.Begin );
		var soundData = memoryStream.ReadBytes( soundDataSize );

		// Gather full byte data of file
		var dataSize = headerSize + soundDataSize;

		// We seek back to offset to capture full data set
		memoryStream.Seek ( offset, SeekOrigin.Begin );
		byte[] data = memoryStream.ReadBytes( dataSize );
		
		return new MP2File( headerSize, fileName, soundData, sampleRate, bitsPerSample, soundType, samples, data );
	}
}
