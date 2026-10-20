using System.Text;

namespace OpenTPW;

/// <summary>
/// A code page ("MBToUni.dat") mapping the game's character codes to UTF-16.
/// </summary>
public sealed class BFMUReader : BaseFormat
{
	/*
	Header
		4 bytes: Magic number - "BFMU"
		2 bytes: Number of lead bytes (0 in English, 54 in Traditional Chinese, 86 in Simplified Chinese)
		2 bytes: Character count in single-byte languages; don't rely on it
	Then one UTF-16 code unit per character until the end of the file.

	Codes start at 1. With L lead bytes, the bytes 256 - L to 255 start two-byte codes:
		single byte c:        character c - 1
		lead l, trail t:      character (255 - L) + (l - (256 - L)) * 255 + (t - 1)
	*/

	/// <summary>
	/// How many of the top byte values start a two-byte code.
	/// </summary>
	public int LeadByteCount { get; private set; }
	public char[] Characters { get; private set; }

	private int FirstLeadByte => 256 - LeadByteCount;

	public BFMUReader( string path )
	{
		ReadFromFile( path );
	}

	public BFMUReader( Stream stream )
	{
		ReadFromStream( stream );
	}

	public void Dispose()
	{
	}

	protected override void ReadFromStream( Stream stream )
	{
		using var reader = new BinaryReader( stream, Encoding.ASCII, true );

		var magicNumber = Encoding.ASCII.GetString( reader.ReadBytes( 4 ) );
		if ( magicNumber != "BFMU" )
			throw new Exception( $"Magic number did not match: {magicNumber}" );

		LeadByteCount = reader.ReadUInt16();
		_ = reader.ReadUInt16();

		var characterCount = (int)(stream.Length - 8) / 2;
		Characters = new char[characterCount];
		for ( int i = 0; i < characterCount; i++ )
			Characters[i] = (char)reader.ReadUInt16();
	}

	public List<char> CharacterArray() => Characters.ToList();

	/// <summary>
	/// The character for a single-byte code.
	/// </summary>
	public char GetCharacter( int character )
	{
		// Characters are offset by 0x01 in the BFMU!
		return Characters[character - 0x01];
	}

	/// <summary>
	/// Decodes a string of character codes, including two-byte codes in languages that use them.
	/// </summary>
	public string Decode( ReadOnlySpan<byte> codes )
	{
		var str = new StringBuilder( codes.Length );

		for ( int i = 0; i < codes.Length; i++ )
		{
			int code = codes[i];
			int index = code - 1;

			if ( LeadByteCount > 0 && code >= FirstLeadByte && i + 1 < codes.Length )
				index = (FirstLeadByte - 1) + (code - FirstLeadByte) * 255 + (codes[++i] - 1);

			str.Append( index >= 0 && index < Characters.Length ? Characters[index] : '?' );
		}

		return str.ToString();
	}
}
