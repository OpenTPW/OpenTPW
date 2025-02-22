using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace OpenTPW;

public class SaveReader : BaseFormat
{
	private ExpandedMemoryStream memoryStream;
	public byte[] buffer;

	public SaveReader( string path )
	{
		using var fileStream = File.OpenRead( path );
		ReadFromStream( fileStream );
	}

	public SaveReader( Stream stream )
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

	public byte[] ReadFile()
	{
		memoryStream.Seek( 0, SeekOrigin.Begin );

		/*
			
		Header
			4 bytes: Magic number - F4 01 00 00
			Copyright notice - 0x0004 to 0x033B
			Padding - 0x033C to 0x0603
			
		File info
			4 bytes: File type (00 01 22 19)
			1 byte: File version (85)
			1 byte: Online flag (00 = offline save, 01 = upload.LAYS)
			2 bytes: Padding
			If online flag set: 	Unknown data - 0x060C to 0x0846
			
		Data	
			## ZLIB Header ##
			4 bytes: Magic number - BILZ
			4 bytes: Unknown
			4 bytes: Compressed length
			16 bytes: Unknown
			2 bytes: ZLIB Compression Header
			ZLIB stream begins after this point, continues to end of file
		*/

		var magicNumber = memoryStream.ReadHex( 4 );

		if ( magicNumber != "F4010000" )
			throw new Exception( $"Magic number did not match: {magicNumber}" );

		int copyrightSize = 824; //Character count with spaces adds to 824

		var copyrightCharacters = memoryStream.ReadChars( copyrightSize );
		string copyright = "";
		foreach ( char c in copyrightCharacters )
		{
			copyright += c;
		}

		memoryStream.Seek( 0x0604, SeekOrigin.Begin );

		var fileType = memoryStream.ReadInt32();

		var fileVersion = memoryStream.ReadByte();
		if ( fileVersion != 133 )
			throw new Exception( $"File version is not 133." );

		// File flag
		bool isOnline = memoryStream.ReadByte() != 0 ? true : false;

		// Padding
		_ = memoryStream.ReadBytes( 2 );

		if ( isOnline )
			throw new Exception( "File is online, no compatibility for this yet." );

		// Padding
		_ = memoryStream.ReadByte();

		// Start of Data
		var dataMagicNumber = memoryStream.ReadString( 4 );

		if ( dataMagicNumber != "BILZ" )
			throw new Exception( $"Magic number did not match: {dataMagicNumber}" );

		// Unknown
		_ = memoryStream.ReadInt32();

		var compressedLength = memoryStream.ReadInt32();

		// Unknown - 16 bytes
		_ = memoryStream.ReadBytes( 16 );

		// get bytes before ZLIB Header
		var initialPos = memoryStream.Position;

		// Compression header
		//var compressionHeader = memoryStream.ReadBytes( 2 );

		byte[] output = new byte[compressedLength];

		using ( MemoryStream uncompressedStream = new MemoryStream() )
		using ( InflaterInputStream compressed = new InflaterInputStream( memoryStream ) )
		{
			compressed.CopyTo( uncompressedStream );
			return uncompressedStream.ToArray();
		}
	}

	/// <summary>
	/// Converts the save file from byte array to a "readable" string
	/// (removes all null entries after string conversion)
	/// </summary>
	/// <returns></returns>
	public string FileToString()
	{
		var output = ReadFile();
		return Encoding.ASCII.GetString( output ).Replace( "\0", string.Empty );
	}
}
