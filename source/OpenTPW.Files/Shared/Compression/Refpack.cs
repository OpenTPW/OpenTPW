using System.Reflection;

namespace OpenTPW;

internal sealed partial class Refpack
{
	private byte[] Data { get; set; }

	public Refpack( byte[] data )
	{
		Data = data;
	}

	public List<byte> Decompress()
	{
		using var stream = new ExpandedMemoryStream( Data );

		var refpackHeader = stream.ReadBytes( 2, bigEndian: true );

		// 0x10: LU01000C - 00010000 - large files & compressed size are not supported.
		if ( refpackHeader[0] != 0xFB || refpackHeader[1] != 0x10 )
			throw new Exception( "Data was not compressed using refpack (header does not match) - possibly corrupted?" );

		// Skip decompressed size
		stream.Seek( 3, SeekOrigin.Current );

		// Refpack is big-endian, unlike the rest of the DWFB format
		// Therefore all memorystream operations have bigEndian set to true
		var decompressedData = new List<byte>();

		var commands = new List<IRefpackCommand>();
		foreach ( var type in Assembly.GetExecutingAssembly().GetTypes() )
		{
			if ( type.GetInterfaces().Contains( typeof( IRefpackCommand ) ) )
			{
				commands.Add( Activator.CreateInstance( type ) as IRefpackCommand );
			}
		}

		var currentByte = stream.ReadBytes( 1, bigEndian: true );
		while ( stream.Position < Data.Length )
		{
			foreach ( var command in commands )
			{
				if ( command.OpcodeMatches( currentByte[0] ) )
				{
					command.Decompress( Data, ref decompressedData, (int)stream.Position - 1, out var skipAhead );
					stream.Seek( command.Length + skipAhead - 1, SeekOrigin.Current );

					if ( command.StopAfterFound )
						stream.Seek( Data.Length, SeekOrigin.Current );
				}
			}

			currentByte = stream.ReadBytes( 1, bigEndian: true );
		}

		return decompressedData;
	}
}
