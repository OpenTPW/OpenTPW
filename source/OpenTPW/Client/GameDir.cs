namespace OpenTPW;

/// <summary>
/// Performs operations that allow the quick, easy access of Theme Park World game files.
/// </summary>
public static class GameDir
{
	private static string? gamePath;

	/// <summary>
	/// The Theme Park World folder (the one with data/): <see cref="Settings.Default.GamePath"/> when
	/// it is set, otherwise the first one <see cref="Find"/> sees from the current directory.
	/// </summary>
	public static string GamePath => gamePath ??= Resolve();

	private static string Resolve()
	{
		var configured = Settings.Default.GamePath;
		if ( !string.IsNullOrWhiteSpace( configured ) )
		{
			if ( !IsGameFolder( configured ) )
				throw new DirectoryNotFoundException( $"GamePath '{configured}' has no data/Language folder" );

			return Path.GetFullPath( configured );
		}

		return Find( Environment.CurrentDirectory ) ?? Find( AppContext.BaseDirectory )
			?? throw new DirectoryNotFoundException( $"Theme Park World not found from {Environment.CurrentDirectory}: "
				+ $"or {AppContext.BaseDirectory}: put the game folder (the one with data/) here or in a folder above, or set GamePath" );
	}

	/// <summary>
	/// Looks for the game folder in <paramref name="start"/> and each folder above it: the folder
	/// itself, then its direct subfolders.
	/// </summary>
	public static string? Find( string start )
	{
		for ( var directory = new DirectoryInfo( start ); directory != null; directory = directory.Parent )
		{
			if ( IsGameFolder( directory.FullName ) )
				return directory.FullName;

			string[] children;
			try
			{
				children = Directory.GetDirectories( directory.FullName );
			}
			catch ( Exception e ) when ( e is UnauthorizedAccessException or IOException )
			{
				continue;
			}

			var match = children.Order( StringComparer.OrdinalIgnoreCase ).FirstOrDefault( IsGameFolder );
			if ( match != null )
				return match;
		}

		return null;
	}

	private static bool IsGameFolder( string path ) => Directory.Exists( Path.Join( path, "data", "Language" ) );

	/// <summary>
	/// Joins <see cref="GamePath"/> to <param name="path">path</param>.
	/// </summary>
	/// <param name="path"></param>
	/// <returns></returns>
	public static string GetPath( string path )
	{
		return Path.Join( GamePath, path ).Replace( "/", "\\" );
	}
}
