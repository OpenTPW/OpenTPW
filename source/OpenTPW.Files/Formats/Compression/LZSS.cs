namespace OpenTPW;

internal static class LZSS
{
	private const int WINDOW_BITS = 12;
	private const int LENGTH_BITS = 7;
	private const int MIN_LENGTH = 1;

	public static bool Decompress( BitReader inputBuffer, BinaryWriter outputBuffer )
	{
		// Pre-allocate buffer for the sliding window
		var buffer = new byte[1 << WINDOW_BITS];  // 4KB buffer based on 12-bit offset
		var stream = outputBuffer.BaseStream;

		try
		{
			while ( true )
			{
				if ( inputBuffer.GetBits( 1 ) == 0 )
				{
					// Literal byte
					outputBuffer.Write( (byte)inputBuffer.GetBits( 8 ) );
					continue;
				}

				// Get match offset and length
				int offset = (int)inputBuffer.GetBits( WINDOW_BITS );
				if ( offset == 0 )
					break;

				int length = (int)inputBuffer.GetBits( LENGTH_BITS ) + MIN_LENGTH;

				if ( offset > stream.Position )
					throw new Exception( "Invalid offset: greater than current stream position" );

				long currentPos = stream.Position;

				// Read the match data into our pre-allocated buffer
				stream.Position = currentPos - offset;
				int bytesRead = stream.Read( buffer, 0, offset );
				stream.Position = currentPos;

				// Write the match data, handling wrap-around
				for ( int i = 0, j = 0; i < length; i++, j++ )
				{
					if ( j >= bytesRead )
						j = 0;  // Wrap around within the actual data we read
					outputBuffer.Write( buffer[j] );
				}
			}

			return true;
		}
		catch
		{
			return false;  // Return false on decompression failure instead of throwing
		}
	}
}
