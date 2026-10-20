using System.Text;

namespace OpenTPW;

/// <summary>
/// A compiled RSSE script ("*.rse"), as the game's loader reads it (TP.exe 0x554E40).
/// </summary>
public sealed class RideScriptFile : BaseFormat
{
	/*
	Header
		4 bytes: Magic number - "RSSE"
		4 bytes: Checksum of the opcode table (0x10F51); the game only warns on a mismatch
		4 bytes: Variable count
		4 bytes: Return stack size
		4 bytes: Time slice (instructions per run; 50 in every shipped script)
		4 bytes: Limbo pool size (8-byte entries)
		4 bytes: Bounce pool size (16-byte entries)
		4 bytes: Walk pool size (32-byte entries)
		16 bytes: "Pad Pad Pad Pad"
		4 bytes: Code word count, then the code words
		4 bytes: String block size, then NUL-terminated strings
		Per variable: 4-byte length and name (debug information; the game doesn't read it)

	Code words: the top byte is a tag, the low 24 bits a value.
		0x80 opcode, 0x40 variable index, 0x20 code word index, 0x10 string block offset,
		0x00 literal (the low 16 bits, signed)
	*/

	public const uint OpcodeTag = 0x80000000;
	public const uint VariableTag = 0x40000000;
	public const uint CodeTag = 0x20000000;
	public const uint StringTag = 0x10000000;
	public const uint TagMask = 0xFF000000;

	/// <summary>
	/// Operand count of each opcode, from the game's opcode table.
	/// </summary>
	public static readonly int[] ArgumentCounts =
	[
		0, 0, 0, 2, 1, 3, 0, 1, 4, 5, 1, 1, 3, 3, 4, 0, 3, 2, 2, 3, 1, 4, 1, 4, 3, 3, 4, 2, 2, 1, 0, 1,
		1, 1, 1, 1, 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 0, 2, 3, 3, 3, 1, 0, 2, 2, 2, 1, 1, 2, 1, 1, 1, 1, 1,
		1, 0, 2, 2, 2, 2, 1, 1, 2, 1, 1, 1, 7, 1, 1, 3, 1, 0, 1, 1, 2, 4, 2, 0, 2, 1, 2, 3, 3, 1, 2, 1,
		1, 1, 1, 1, 1, 1, 1, 1, 1, 4
	];

	public uint Checksum { get; private set; }
	public int VariableCount { get; private set; }
	public int StackSize { get; private set; }
	public int TimeSlice { get; private set; }
	public int LimboSize { get; private set; }
	public int BounceSize { get; private set; }
	public int WalkSize { get; private set; }
	public uint[] Code { get; private set; }
	public byte[] StringBlock { get; private set; }
	public string[] VariableNames { get; private set; }

	public RideScriptFile( string path )
	{
		ReadFromFile( path );
	}

	public RideScriptFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	protected override void ReadFromStream( Stream stream )
	{
		using var reader = new BinaryReader( stream, Encoding.ASCII, true );

		var magicNumber = Encoding.ASCII.GetString( reader.ReadBytes( 4 ) );
		if ( magicNumber != "RSSE" )
			throw new Exception( $"Magic number did not match: {magicNumber}" );

		Checksum = reader.ReadUInt32();
		VariableCount = reader.ReadInt32();
		StackSize = reader.ReadInt32();
		TimeSlice = reader.ReadInt32();
		LimboSize = reader.ReadInt32();
		BounceSize = reader.ReadInt32();
		WalkSize = reader.ReadInt32();
		_ = reader.ReadBytes( 16 );

		Code = new uint[reader.ReadInt32()];
		for ( int i = 0; i < Code.Length; i++ )
			Code[i] = reader.ReadUInt32();

		StringBlock = reader.ReadBytes( reader.ReadInt32() );

		VariableNames = new string[VariableCount];
		for ( int i = 0; i < VariableCount && stream.Position + 4 <= stream.Length; i++ )
			VariableNames[i] = Encoding.ASCII.GetString( reader.ReadBytes( reader.ReadInt32() ) ).TrimEnd( '\0' );
	}

	/// <summary>
	/// The string at a byte offset in the string block (a string operand's value).
	/// </summary>
	public string GetString( int offset )
	{
		if ( offset < 0 || offset >= StringBlock.Length )
			return "";

		int end = Array.IndexOf( StringBlock, (byte)0, offset );
		return Encoding.ASCII.GetString( StringBlock, offset, (end < 0 ? StringBlock.Length : end) - offset );
	}
}
