using System.Text;

namespace OpenTPW;
public sealed class BFMUReader : BaseFormat
{
	private ExpandedMemoryStream memoryStream;
	public byte[] buffer;

	public BFMUReader( string path )
	{
		ReadFromFile( path );
	}

	public BFMUReader( Stream stream )
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

		var magicNumber = memoryStream.ReadString( 4 );

		if ( magicNumber != "BFMU" )
			throw new Exception( $"Magic number did not match: {magicNumber}" );

		//ReadFile();
		//CharacterArray();
	}

	public List<char> CharacterArray()
	{
		List<char> allCharacters = new List<char>();

		// Skip Header
		memoryStream.Seek( 6, SeekOrigin.Begin );

		// Read Character Length
		var charCount = memoryStream.ReadByte();
		
		//Skip 0x0 unsued byte
		_ = memoryStream.ReadByte();

		for ( int i = 0; i < charCount; i++ )
		{
			var currentChar = memoryStream.ReadBytes(2);
			foreach(var character in Encoding.Unicode.GetChars( currentChar ) )
			{
				allCharacters.Add( character );
			}
		}
		return allCharacters;
	}

	public void ReadFile()
	{
		/*
		
		Header

		4 bytes: Magic number - "BFMU"
		2 bytes: Likely specifies the character encoding - usually 0x00
		2 bytes: Character count

		# For each character

		2 bytes - The character itself in either Unicode or multibyte form

		*/

		var encoding = memoryStream.ReadBytes( 2 );
		Log.Info($"Encoding: {encoding}", true );
		var charCount = memoryStream.ReadBytes( 2 );

		for ( int i = 0; i < memoryStream.Length - memoryStream.Position; i++ )
		{
			//Log.Info( $"Character: {GetCharacter( memoryStream.Position )} @ Position: {memoryStream.Position}", true );
			_= memoryStream.ReadByte();
		}

	}

	public char GetCharacter(int character)
	{
		var array = CharacterArray();
		
		// Characters are offset by 0x01 in the BFMU!
		return array[character - 0x01];
	}

}
