namespace OpenTPW;

// BIG SHOUT TO Toksisitee - https://github.com/Toksisitee/PopSoundEditor

public class SoundFile : BaseFormat, IDisposable
{
	private MemoryStream memoryStream = null!;
	public byte[] buffer = null!;

	public SoundFile( string path ) => ReadFromFile( path );
	public SoundFile( Stream stream ) => ReadFromStream( stream );
	public void Dispose() => memoryStream.Dispose();

	protected override void ReadFromStream( Stream stream )
	{
		using var copy = new MemoryStream();
		stream.CopyTo( copy );
		buffer = copy.ToArray();
		memoryStream = new MemoryStream( buffer, writable: false );
	}

	public MP2File GetFile( MemoryStream stream, int offset = 0, bool dataOnly = false )
		=> ReadEntry( memoryStream, offset, dataOnly );

	internal static MP2File ReadEntry( Stream stream, long offset, bool dataOnly = false )
	{
		const int MinimumHeaderSize = 40;
		if ( offset < 0 || offset > stream.Length - MinimumHeaderSize )
			throw new InvalidDataException( "SDT sound header is outside the bank." );
		stream.Position = offset;
		using var reader = new BinaryReader( stream, System.Text.Encoding.ASCII, leaveOpen: true );
		int headerSize = reader.ReadInt32();
		int size = reader.ReadInt32();
		if ( headerSize < MinimumHeaderSize || headerSize > stream.Length - offset ||
			size < 0 || size > stream.Length - offset - headerSize )
			throw new InvalidDataException( "Invalid SDT sound size." );
		string name = System.Text.Encoding.ASCII.GetString( reader.ReadExactBytes( 16 ) ).TrimEnd( '\0' );
		name = Path.ChangeExtension( name, ".mp2" );
		// A ushort rate, byte resolution and byte type share these four bytes.
		int rate = reader.ReadUInt16();
		int bits = reader.ReadByte();
		int type = reader.ReadByte();
		_ = reader.ReadInt32();
		int samples = reader.ReadInt32();
		_ = reader.ReadInt32();
		stream.Position = offset + headerSize;
		byte[] sound = reader.ReadExactBytes( size );
		byte[] data = [];
		if ( !dataOnly )
		{
			stream.Position = offset;
			data = reader.ReadExactBytes( checked( headerSize + size ) );
		}
		return new MP2File( headerSize, name, sound, rate, bits, type, samples, data );
	}
}
