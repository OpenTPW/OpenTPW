using System.Globalization;
using System.Text;

namespace OpenTPW;

/// <summary>
/// A level's texture correspondence table (<c>terrain.wad/&lt;theme&gt;.tct</c>, e.g. Jungle.tct; Theme Park Inc's levels
/// ship the same format): section names on their own line, each followed by "&lt;index&gt; &lt;file&gt;" rows. The
/// <c>PathTex</c> section lists the path tile textures that a map cell's tile number indexes. Lines starting with '#'
/// are comments. Rows keep only the file stem, since textures are looked up by stem.
/// </summary>
public sealed class TctFile
{
	public IReadOnlyDictionary<string, IReadOnlyList<string>> Sections { get; }

	public TctFile( Stream stream ) : this( ReadText( stream ) ) { }

	public TctFile( string text )
	{
		var sections = new Dictionary<string, List<string>>( StringComparer.Ordinal );
		List<string>? current = null;
		foreach ( var raw in text.Split( '\n' ) )
		{
			var line = raw.TrimEnd( '\r' );
			if ( line.Length == 0 || line[0] == '#' ) continue;
			var words = line.Split( [' ', '\t'], StringSplitOptions.RemoveEmptyEntries );
			if ( words.Length == 0 ) continue;
			if ( words.Length >= 2 && int.TryParse( words[0], NumberStyles.None, CultureInfo.InvariantCulture, out int index ) )
			{
				// A row before any section header has no table to belong to.
				if ( current == null ) continue;
				while ( current.Count <= index ) current.Add( "" );
				current[index] = Path.GetFileNameWithoutExtension( words[1].Replace( '\\', '/' ).Split( '/' )[^1] );
			}
			else if ( !sections.TryGetValue( words[0], out current ) )
			{
				sections[words[0]] = current = [];
			}
		}
		Sections = sections.ToDictionary( pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal );
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
