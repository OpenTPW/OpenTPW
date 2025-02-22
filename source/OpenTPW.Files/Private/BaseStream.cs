using System.Text;

namespace OpenTPW;

internal class BaseStream : MemoryStream
{
	public BaseStream( byte[] buffer ) : base( buffer ) { }
	public byte[] ReadBytes( int length, bool bigEndian = false )
	{
		var bytes = new byte[length];
		Read( bytes, 0, length );

		if ( bigEndian )
			Array.Reverse( bytes );
		return bytes;
	}

	public char[] ReadChars( int length, bool bigEndian = false )
	{
		return Encoding.ASCII.GetChars( ReadBytes( length, bigEndian ) );
	}

	public string ReadString( int length, bool bigEndian = false )
	{
		return Encoding.ASCII.GetString( ReadBytes( length, bigEndian ) );
	}

	public string ReadHex( int length, bool bigEndian = false )
	{
		return Convert.ToHexString( ReadBytes( length, bigEndian ) );
	}

	public int ReadInt16( bool bigEndian = false )
	{
		return BitConverter.ToInt16( ReadBytes( 4, bigEndian ), 0 );
	}

	public int ReadInt32( bool bigEndian = false )
	{
		return BitConverter.ToInt32( ReadBytes( 4, bigEndian ), 0 );
	}

	public uint ReadUInt32( bool bigEndian = false )
	{
		return BitConverter.ToUInt32( ReadBytes( 4, bigEndian ), 0 );
	}

	public uint ReadUIntN( int n, bool bigEndian = false )
	{
		return BitConverter.ToUInt32( ReadBytes( n, bigEndian ), 0 );
	}
}
