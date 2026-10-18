namespace OpenTPW;

public static class BinaryReaderExtensions
{
	/// <summary>Read a complete block. An optional message reports malformed files as InvalidDataException.</summary>
	public static byte[] ReadExactBytes( this BinaryReader reader, int count, string? invalidDataMessage = null )
	{
		var bytes = reader.ReadBytes( count );
		if ( bytes.Length != count )
		{
			if ( invalidDataMessage != null )
			{
				throw new InvalidDataException( invalidDataMessage );
			}

			throw new EndOfStreamException();
		}

		return bytes;
	}
}
