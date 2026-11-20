using System.Globalization;
using System.Text;

namespace OpenTPW;

/// <summary>
/// A level's texture correspondence table (<c>terrain.wad/&lt;theme&gt;.tct</c>, e.g. Jungle.tct; Theme Park Inc's levels
/// ship the same format): section names on their own line, each followed by "&lt;index&gt; &lt;file&gt;" rows. The
/// <c>PathTex</c> section lists the path tile textures that a map cell's tile number indexes. Lines starting with '#'
/// or ';' are comments (Tct_Read, US 0x427620 / TPI 0x460a10). Rows keep only the file stem, since textures are looked
/// up by stem.
/// </summary>
public sealed class TctFile
{
	public IReadOnlyDictionary<string, IReadOnlyList<string>> Sections { get; }

	/// <summary>
	/// The rows of each section in the order the file lists them, row numbers ignored. Tct_Read stores the number but
	/// appends every row to its table, and Terrain_WriteCellTile (US 0x46df50 / TPI 0x4d7cf0) picks by position.
	/// </summary>
	public IReadOnlyDictionary<string, IReadOnlyList<string>> SectionsInFileOrder { get; }

	public TctFile( Stream stream ) : this( ReadText( stream ) ) { }

	public TctFile( string text )
	{
		var sections = new Dictionary<string, List<string>>( StringComparer.Ordinal );
		var inOrder = new Dictionary<string, List<string>>( StringComparer.Ordinal );
		List<string>? current = null, currentInOrder = null;
		foreach ( var raw in text.Split( '\n' ) )
		{
			var line = raw.TrimEnd( '\r' );
			if ( line.Length == 0 || line[0] == '#' || line[0] == ';' ) continue;
			var words = line.Split( [' ', '\t'], StringSplitOptions.RemoveEmptyEntries );
			if ( words.Length == 0 ) continue;
			if ( words.Length >= 2 && int.TryParse( words[0], NumberStyles.None, CultureInfo.InvariantCulture, out int index ) )
			{
				// A row before any section header has no table to belong to.
				if ( current == null ) continue;
				var stem = Path.GetFileNameWithoutExtension( words[1].Replace( '\\', '/' ).Split( '/' )[^1] );
				while ( current.Count <= index ) current.Add( "" );
				current[index] = stem;
				currentInOrder!.Add( stem );
			}
			else if ( !sections.TryGetValue( words[0], out current ) )
			{
				sections[words[0]] = current = [];
				inOrder[words[0]] = currentInOrder = [];
			}
			else
			{
				currentInOrder = inOrder[words[0]];
			}
		}
		Sections = sections.ToDictionary( pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal );
		SectionsInFileOrder = inOrder.ToDictionary( pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal );
	}

	/// <summary>The rows of <paramref name="section"/> by index ("" for gaps), or an empty list.</summary>
	public IReadOnlyList<string> this[string section] => Sections.TryGetValue( section, out var rows ) ? rows : [];

	// The tables are 8-bit text; Latin-1 keeps every byte as one character.
	private static string ReadText( Stream stream )
	{
		using var reader = new StreamReader( stream, Encoding.Latin1, detectEncodingFromByteOrderMarks: false, leaveOpen: true );
		return reader.ReadToEnd();
	}
}
