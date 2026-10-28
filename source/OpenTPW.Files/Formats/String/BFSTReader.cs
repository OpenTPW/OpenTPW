using System.Text;

namespace OpenTPW;

internal sealed class BFSTReader : BaseFormat
{
	private ExpandedMemoryStream memoryStream;
	public byte[] buffer;

	private static BFMUReader LookupTable = new BFMUReader( GameLanguage.Path( "MBToUni.dat" ) );

	public BFSTReader( string path )
	{
		ReadFromFile( path );
	}

	public BFSTReader( Stream stream )
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

		ReadFile();
	}

	public string[] ReadFile()
	{
		memoryStream.Seek( 0, SeekOrigin.Begin );

		/*	
		Header
			4 bytes: Magic number - "BFST"
			4 bytes: Unknown
			4 bytes: String count
			
		#For each string
			4 bytes - String offset (from the end of the string count)
			
		#For each string (at offset)
			1 byte - Unknown, always 0x01
			3 bytes - String length
			n bytes - Each character, specified with an offset in a BFMU file.
			4 bytes - Padding (may be longer?)
		*/

		var magicNumber = memoryStream.ReadString( 4 );

		if ( magicNumber != "BFST" )
			throw new Exception( $"Magic number did not match: {magicNumber}" );

		//Unknown
		_ = memoryStream.ReadInt32();


		//String Count
		var stringCount = memoryStream.ReadInt32();

		//Save Pos and create
		var initialMemPos = memoryStream.Position;
		List<string> outputList = new List<string>();

		for ( int i = 0; i < stringCount; i++ )
		{
			// Go to next offset based on iteration
			memoryStream.Seek( 4 * (i), SeekOrigin.Current );

			// Find offset number
			int offset = memoryStream.ReadInt32();

			// go to offset address
			// need to account offset for inital 12 bytes
			memoryStream.Seek( offset + 12, SeekOrigin.Begin );

			// Always 0x1, except for empty entries, which are four zero bytes
			// (e.g. #140 in the Traditional Chinese TAG_SYSTEM.str)
			var unknownOne = memoryStream.ReadByte();

			// String length in bytes (24 bits; UITEXT.str has strings over 255 bytes)
			var stringLength = memoryStream.ReadByte() | (memoryStream.ReadByte() << 8) | (memoryStream.ReadByte() << 16);

			if ( unknownOne != 1 && !(unknownOne == 0 && stringLength == 0) )
			{
				throw new Exception( $"String Offset not working - offset set to {unknownOne} @ Pos:{memoryStream.Position}" );
			}

			// Characters, one or two bytes each
			var codes = memoryStream.ReadBytes( stringLength );
			outputList.Add( LookupTable.Decode( codes ) );

			// Go back to intial position
			memoryStream.Seek( initialMemPos, SeekOrigin.Begin );

		}

		return outputList.ToArray();
	}
}
