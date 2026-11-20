namespace OpenTPW;

/// <summary>Shared RefPack entry point for compressed WAD members and SHPI image records.</summary>
public static class RefPack
{
	/// <summary>Decodes the existing 0x10FB variant and checks its declared output length.</summary>
	public static byte[] Decompress( byte[] data )
	{
		ArgumentNullException.ThrowIfNull( data );
		if ( data.Length < 6 || data[0] != 0x10 || data[1] != 0xfb )
			throw new InvalidDataException( "Unsupported or truncated RefPack header" );
		try
		{
			var result = new Refpack( data ).Decompress().ToArray();
			var expected = (data[2] << 16) | (data[3] << 8) | data[4];
			if ( result.Length != expected ) throw new InvalidDataException( $"RefPack output length {result.Length} differs from {expected}" );
			return result;
		}
		catch ( Exception e ) when ( e is IndexOutOfRangeException or ArgumentOutOfRangeException or EndOfStreamException )
		{
			throw new InvalidDataException( "Invalid RefPack command data", e );
		}
	}
}
