using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>TP.exe 0x6ba210..0x6ba6e5: packed category tables, before pointer relocation.</summary>
public sealed class SoundCategoryMap
{
	/// <summary>SDT sample reference; <c>Bank</c> and <c>Id</c> are 1-based (0x6ba5f1), 0 means unbound.</summary>
	public sealed record Sample( uint Id, uint Threshold, uint DurationMilliseconds, ushort Bank, ushort Flags );
	public sealed record Relation( uint Definition, uint Parameters );
	public sealed record Definition( uint Weight, byte VolumeMin, byte VolumeMax, ushort DelayMin, ushort DelayMax,
		IReadOnlyList<Sample> Samples, IReadOnlyList<Relation> Relations, byte[] Raw );
	/// <summary>A sound ID's definitions; <c>Priority</c> becomes the high 16 bits of the voice priority (0x6b5b00).</summary>
	public sealed record Sound( uint Id, uint Priority, ushort Flags, IReadOnlyList<Definition> Definitions );
	/// <summary>A BANK.map entry: SDT path stem relative to the category's parent directory.</summary>
	public sealed record Bank( string Path, byte[] Raw );

	public IReadOnlyList<Bank> Banks { get; }
	public IReadOnlyDictionary<uint, Sound> Sounds { get; }

	private SoundCategoryMap( Bank[] banks, Dictionary<uint, Sound> sounds )
	{
		Banks = Array.AsReadOnly( banks );
		Sounds = new System.Collections.ObjectModel.ReadOnlyDictionary<uint, Sound>( sounds );
	}

	/// <summary>Bounds-checked cursor; every table is taken in file order before pointers are resolved.</summary>
	private sealed class Reader
	{
		internal readonly byte[] Data;
		internal int Position;
		internal Reader( byte[] data ) => Data = data;
		internal int Take( long size )
		{
			if ( size < 0 || size > Data.LongLength - Position ) throw new InvalidDataException( $"Truncated sound map at {Position:X}" );
			int start = Position;
			Position += (int)size;
			return start;
		}
		internal int Array( uint count, int stride ) => Take( (long)count * stride );
		internal ushort U16( int p ) => BinaryPrimitives.ReadUInt16LittleEndian( Data.AsSpan( p, 2 ) );
		internal uint U32( int p ) => BinaryPrimitives.ReadUInt32LittleEndian( Data.AsSpan( p, 4 ) );
		internal uint Header( bool bank )
		{
			Take( 28 );
			byte[] signature = Convert.FromHexString( bank ? "012C61E9D031D211B40900A0C993F203" : "002C61E9D031D211B40900B0C993F203" );
			if ( !Data.AsSpan( 0, 16 ).SequenceEqual( signature ) ) throw new InvalidDataException( "Invalid sound map signature" );
			return U32( 24 );
		}
		internal void Finish()
		{
			if ( Position != Data.Length ) throw new InvalidDataException( $"Sound map has trailing bytes at {Position:X}" );
		}
	}

	public static SoundCategoryMap Parse( byte[] bankData, byte[] soundData )
	{
		var bank = new Reader( bankData );
		uint bankCount = bank.Header( true );
		int bankRecords = bank.Array( bankCount, 11 );
		var banks = new Bank[bankCount];
		for ( int i = 0; i < banks.Length; ++i )
		{
			uint size = bank.U32( bank.Take( 4 ) );
			int text = bank.Take( size );
			if ( size == 0 || bankData[text + (int)size - 1] != 0 ) throw new InvalidDataException( "Unterminated sound bank path" );
			string path = Encoding.ASCII.GetString( bankData, text, (int)size - 1 ).Replace( '\\', '/' );
			if ( path.StartsWith( '/' ) || path.Contains( ':' ) || path.Split( '/' ).Any( part => part is ".." or "." or "" ) )
				throw new InvalidDataException( $"Invalid sound bank path {path}" );
			banks[i] = new( path, bankData.AsSpan( bankRecords + i * 11, 11 ).ToArray() );
		}
		bank.Finish();

		var reader = new Reader( soundData );
		uint groupCount = reader.Header( false );
		int groups = reader.Array( groupCount, 24 );
		bool cumulative = reader.U32( 16 ) == 0;
		var sounds = new Dictionary<uint, Sound>();
		for ( int group = 0; group < groupCount; ++group )
		{
			uint count = reader.U32( groups + group * 24 );
			int records = reader.Array( count, 20 );
			for ( int s = 0; s < count; ++s )
			{
				int record = records + s * 20;
				uint definitionCount = reader.U32( record + 4 );
				int definitionRecords = reader.Array( definitionCount, 42 );
				var definitions = new Definition[definitionCount];
				uint previous = 0;
				for ( int d = 0; d < definitions.Length; ++d )
				{
					int p = definitionRecords + d * 42;
					uint weight = reader.U32( p + 30 );
					uint storedWeight = weight;
					if ( cumulative )
					{
						if ( weight < previous ) throw new InvalidDataException( "Decreasing sound definition weights" );
						weight -= previous;
					}
					previous = storedWeight;
					var samples = new Sample[reader.U16( p )];
					int sampleRecords = reader.Array( (uint)samples.Length, 16 );
					for ( int n = 0; n < samples.Length; ++n )
					{
						int q = sampleRecords + n * 16;
						ushort reference = reader.U16( q + 12 );
						if ( reference > banks.Length ) throw new InvalidDataException( "Sound sample references a nonexistent bank" );
						samples[n] = new( reader.U32( q ), reader.U32( q + 4 ), reader.U32( q + 8 ), reference, reader.U16( q + 14 ) );
					}
					uint relationCount = reader.U32( p + 4 );
					int relationRecords = reader.Array( relationCount, 8 );
					var relations = new Relation[relationCount];
					for ( int n = 0; n < relations.Length; ++n )
					{
						int q = relationRecords + n * 8;
						uint reference = reader.U32( q );
						if ( reference == 0 || reference > definitions.Length ) throw new InvalidDataException( "Invalid sound definition relation" );
						relations[n] = new( reference, reader.U32( q + 4 ) );
					}
					definitions[d] = new( weight, soundData[p + 12], soundData[p + 13], reader.U16( p + 16 ), reader.U16( p + 18 ),
						Array.AsReadOnly( samples ), Array.AsReadOnly( relations ), soundData.AsSpan( p, 42 ).ToArray() );
				}
				uint id = reader.U32( record );
				if ( !sounds.TryAdd( id, new( id, reader.U32( record + 12 ), reader.U16( record + 16 ), Array.AsReadOnly( definitions ) ) ) )
					throw new InvalidDataException( $"Duplicate sound ID {id}" );
			}
		}
		reader.Finish();
		return new( banks, sounds );
	}
}
