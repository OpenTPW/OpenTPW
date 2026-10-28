namespace OpenTPW;

/// <summary>
/// The folder under data/Language that strings, bitmap fonts and the multibyte table come from.
/// Retail picks it from the registry Language LCID (0x409 American, 0x809 English, 0x404
/// TraditionalChinese, ...); OpenTPW takes the folder name instead, so an installed language and a
/// language pack are chosen the same way.
/// </summary>
public static class GameLanguage
{
	public const string Default = "English";

	/// <summary>Folder name under data/Language, as spelled on disk. English until <see cref="Select"/>.</summary>
	public static string Name { get; private set; } = Default;

	/// <summary>Path of a file in the selected language folder, relative to data/.</summary>
	public static string Path( string file ) => $"Language/{Name}/{file}";

	public static void Select( string name ) => Name = string.IsNullOrWhiteSpace( name )
		? throw new ArgumentException( "Language folder name is empty", nameof(name) ) : name;

	/// <summary>
	/// The requested folder when it exists (any case); without a request, the language pack's only
	/// folder, then American (only US installs carry it, matching their 0x409 registry value), then English,
	/// otherwise the only folder. Anything else is ambiguous or missing and throws.
	/// </summary>
	public static string Resolve( IEnumerable<string> available, string? requested, IEnumerable<string>? pack = null )
	{
		var folders = available.ToArray();
		string? Find( string name ) => folders.FirstOrDefault( folder => folder.Equals( name, StringComparison.OrdinalIgnoreCase ) );
		if ( !string.IsNullOrWhiteSpace( requested ) )
			return Find( requested.Trim() ) ?? throw new DirectoryNotFoundException(
				$"Language '{requested}' not found in data/Language (available: {string.Join( ", ", folders )})" );
		var packFolders = pack?.ToArray() ?? Array.Empty<string>();
		if ( packFolders.Length == 1 ) return Find( packFolders[0] ) ?? packFolders[0];
		if ( packFolders.Length > 1 ) throw new DirectoryNotFoundException(
			$"The language pack has several languages; set Language to one of: {string.Join( ", ", packFolders )}" );
		if ( Find( "American" ) is string american ) return american;
		if ( Find( Default ) is string english ) return english;
		if ( folders.Length == 1 ) return folders[0];
		throw new DirectoryNotFoundException( folders.Length == 0
			? "data/Language has no language folder"
			: $"data/Language has several languages and no English; set Language to one of: {string.Join( ", ", folders )}" );
	}

	/// <summary>
	/// The data folder of a language pack: the folder itself when it has Language/, else its data/ child
	/// (retail packs are copied into the game folder as data/).
	/// </summary>
	public static string PackDataFolder( string pack )
	{
		var full = System.IO.Path.GetFullPath( pack );
		if ( Directory.Exists( System.IO.Path.Join( full, "Language" ) ) ) return full;
		var data = System.IO.Path.Join( full, "data" );
		if ( Directory.Exists( data ) ) return data;
		throw new DirectoryNotFoundException( $"Language pack has no data folder: {full}" );
	}

	/// <summary>Language folder names directly in a pack's data folder (none for packs that only replace speech or art).</summary>
	public static string[] PackLanguages( string packData ) => Directory.Exists( System.IO.Path.Join( packData, "Language" ) )
		? Directory.GetDirectories( System.IO.Path.Join( packData, "Language" ) ).Select( System.IO.Path.GetFileName ).OfType<string>().ToArray()
		: Array.Empty<string>();

	/// <summary>Language folder names present in <paramref name="files"/> (rooted at data/).</summary>
	public static string[] Available( BaseFileSystem files ) => files.GetDirectories( "Language" )
		.Select( directory => System.IO.Path.GetFileName( directory.TrimEnd( '/', '\\' ) ) ).ToArray();
}
