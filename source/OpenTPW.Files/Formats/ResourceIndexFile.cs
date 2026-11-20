using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;

namespace OpenTPW;

/// <summary>The BFRI resource-ID directory in each retail language pack (residx.dat).</summary>
public sealed class ResourceIndexFile
{
	/// <summary>A resource's host-defined type and relative language-pack filename.</summary>
	public sealed record Entry( uint Id, ushort Type, string Name );
	public IReadOnlyDictionary<uint, Entry> Entries { get; }
	public Entry this[uint id] => Entries[id];

	public ResourceIndexFile( Stream stream )
	{
		using var copy = new MemoryStream();
		stream.CopyTo( copy );
		byte[] bytes = copy.ToArray();
		if ( bytes.Length < 8 || !bytes.AsSpan( 0, 4 ).SequenceEqual( "BFRI"u8 ) )
			throw new InvalidDataException( "Invalid BFRI resource index header." );
		// The 'K' in the Inc header is the low byte of its record count (75), not part of the magic.
		uint count = BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( 4 ) );
		if ( count > (bytes.Length - 8) / 12 )
			throw new InvalidDataException( "Invalid resource index table boundary." );
		int names = 8 + (int)count * 12;
		var entries = new Dictionary<uint, Entry>();
		for ( int offset = 8; offset < names; offset += 12 )
		{
			var record = bytes.AsSpan( offset, 12 );
			uint id = BinaryPrimitives.ReadUInt32LittleEndian( record );
			ushort type = BinaryPrimitives.ReadUInt16LittleEndian( record[4..] );
			int start = 8 + BinaryPrimitives.ReadUInt16LittleEndian( record[6..] );
			if ( BinaryPrimitives.ReadUInt32LittleEndian( record[8..] ) != 0 || start < names || start >= bytes.Length )
				throw new InvalidDataException( $"Invalid resource index record {id:x}." );
			int end = Array.IndexOf( bytes, (byte)0, start );
			if ( end <= start || bytes.AsSpan( start, end - start ).ContainsAnyInRange( (byte)128, byte.MaxValue ) )
				throw new InvalidDataException( $"Invalid resource filename for {id:x}." );
			string name = Encoding.ASCII.GetString( bytes, start, end - start );
			if ( !entries.TryAdd( id, new Entry( id, type, name ) ) )
				throw new InvalidDataException( $"Duplicate resource ID {id:x}." );
		}
		Entries = new ReadOnlyDictionary<uint, Entry>( entries );
	}
}
