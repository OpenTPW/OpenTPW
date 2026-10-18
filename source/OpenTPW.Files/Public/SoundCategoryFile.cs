using System.Text;

namespace OpenTPW;

/// <summary>The sound category MAP files are separate from terrain MAP files.</summary>
public static class SoundCategoryFile
{
	public sealed record Sample( int Number, int Bank, ushort Threshold );
	public sealed record Category( int Id, IReadOnlyList<Sample[]> Groups );
	private const int GroupSize = 42;
	private const int SampleSize = 16;
	private const int RangeSize = 8;

	public static Category[] ReadCategories( byte[] data )
	{
		using var stream = new MemoryStream( data, writable: false );
		using var reader = new BinaryReader( stream );
		ReadHeader( reader, bank: false );
		reader.ReadExactBytes( 4 );
		int count = ReadCount( reader );
		reader.ReadExactBytes( 20 );
		// Category descriptors (20 bytes each) precede all group/sample data.
		var descriptors = new (int Id, int Groups)[count];
		for ( int i = 0; i < count; i++ )
		{
			descriptors[i] = (reader.ReadInt32(), ReadCount( reader ));
			reader.ReadExactBytes( 12 );
		}
		var result = new Category[count];
		for ( int i = 0; i < count; i++ )
		{
			var sizes = new (int Samples, int Ranges)[descriptors[i].Groups];
			for ( int g = 0; g < sizes.Length; g++ )
			{
				sizes[g] = (ReadCount( reader ), ReadCount( reader ));
				reader.ReadExactBytes( GroupSize - 8 );
			}
			var groups = new Sample[sizes.Length][];
			for ( int g = 0; g < groups.Length; g++ )
			{
				groups[g] = new Sample[sizes[g].Samples];
				for ( int s = 0; s < groups[g].Length; s++ )
				{
					int number = reader.ReadInt32();
					ushort threshold = reader.ReadUInt16();
					reader.ReadExactBytes( SampleSize - 10 );
					int bank = reader.ReadInt32();
					if ( number < 1 || bank < 1 )
						throw new InvalidDataException( "Invalid sound category sample reference." );
					groups[g][s] = new( number, bank, threshold );
				}
				reader.ReadExactBytes( checked( sizes[g].Ranges * RangeSize ) );
			}
			result[i] = new( descriptors[i].Id, groups );
		}
		if ( stream.Position != stream.Length )
			throw new InvalidDataException( "Unexpected sound category data." );
		return result;
	}

	public static string[] ReadBanks( byte[] data )
	{
		using var stream = new MemoryStream( data, writable: false );
		using var reader = new BinaryReader( stream );
		ReadHeader( reader, bank: true );
		int count = ReadCount( reader );
		reader.ReadExactBytes( checked( count * 11 ) );
		var banks = new string[count];
		for ( int i = 0; i < count; i++ )
		{
			int size = reader.ReadInt32();
			if ( size < 2 || size > 1024 )
				throw new InvalidDataException( "Invalid sound bank name length." );
			var bytes = reader.ReadExactBytes( size );
			if ( bytes[^1] != 0 )
				throw new InvalidDataException( "Sound bank name is not terminated." );
			banks[i] = Encoding.ASCII.GetString( bytes, 0, size - 1 ).Replace( '\\', '/' );
			if ( banks[i].StartsWith( '/' ) || banks[i].Contains( ':' ) || banks[i].Split( '/' ).Any( p => p is ".." or "." or "" ) )
				throw new InvalidDataException( "Invalid sound bank path." );
		}
		return banks;
	}

	private static void ReadHeader( BinaryReader reader, bool bank )
	{
		byte[] expected = Convert.FromHexString( bank ? "012C61E9D031D211B40900A0C993F203" : "002C61E9D031D211B40900B0C993F203" );
		if ( !reader.ReadExactBytes( 16 ).SequenceEqual( expected ) )
			throw new InvalidDataException( "Invalid sound category signature." );
		reader.ReadExactBytes( 8 );
	}

	private static int ReadCount( BinaryReader reader )
	{
		int count = reader.ReadInt32();
		if ( count < 0 || count > 65535 || count > reader.BaseStream.Length - reader.BaseStream.Position )
			throw new InvalidDataException( "Invalid sound category count." );
		return count;
	}
}
