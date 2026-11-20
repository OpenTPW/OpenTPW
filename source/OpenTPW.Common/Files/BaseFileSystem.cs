using System.Text;

namespace OpenTPW;

/// <summary>
/// Game files under a root folder, with registered archive types (WAD, SDT) readable as folders.
/// Overlay roots added with <see cref="AddOverlay"/> are searched first, file by file, which is how a
/// retail language pack works: its data folder is copied over the installed game's data.
/// </summary>
public class BaseFileSystem
{
	private readonly string basePath;
	// Highest priority first; the base path is searched last.
	private readonly List<string> overlays = new();
	private readonly Dictionary<string, Type> archiveHandlers = new();
	private readonly Dictionary<string, IArchive> archiveCache = new();

	public BaseFileSystem( string relativePath )
	{
		if ( !Directory.Exists( relativePath ) )
			Directory.CreateDirectory( relativePath );

		basePath = Path.GetFullPath( relativePath, Directory.GetCurrentDirectory() );
	}

	/// <summary>Searches <paramref name="path"/> before the earlier roots. An archive in an overlay replaces the whole archive.</summary>
	public void AddOverlay( string path )
	{
		var full = Path.GetFullPath( path );
		if ( !Directory.Exists( full ) ) throw new DirectoryNotFoundException( $"Overlay folder not found: {full}" );
		overlays.Insert( 0, full );
	}

	public void RegisterArchiveHandler<T>( string extension ) where T : IArchive
	{
		archiveHandlers[extension] = typeof( T );
	}

	public string ReadAllText( string relativePath )
	{
		using var stream = OpenRead( relativePath );
		using var reader = new StreamReader( stream, Encoding.ASCII );
		return reader.ReadToEnd();
	}

	public byte[] ReadAllBytes( string relativePath )
	{
		using var stream = OpenRead( relativePath );
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		return ms.ToArray();
	}

	public bool FileExists( string relativePath )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		return File.Exists( absolutePath );
	}

	public bool DirectoryExists( string relativePath )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		return Directory.Exists( absolutePath );
	}

	public Stream OpenWrite( string relativePath )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		var (archivePath, _) = FindArchivePath( absolutePath );

		if ( !string.IsNullOrEmpty( archivePath ) )
		{
			throw new NotImplementedException( "Can't write to archives" );
		}

		string? directoryName = Path.GetDirectoryName( absolutePath );
		if ( !Directory.Exists( directoryName ) )
			Directory.CreateDirectory( directoryName );

		return File.OpenWrite( absolutePath );
	}

	public Stream OpenRead( string relativePath )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		var (archivePath, internalPath) = FindArchivePath( absolutePath );

		if ( !string.IsNullOrEmpty( archivePath ) )
		{
			var archive = GetArchive( archivePath );
			return archive?.OpenFile( internalPath );
		}

		return File.Open( absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read );
	}

	public long GetSize( string relativePath )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		var (archivePath, internalPath) = FindArchivePath( absolutePath );

		if ( !string.IsNullOrEmpty( archivePath ) )
		{
			var archive = GetArchive( archivePath );
			return archive?.GetFileSize( internalPath ) ?? 0L;
		}

		return new FileInfo( absolutePath ).Length;
	}

	public DateTime GetModifiedTime( string relativePath )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		var (archivePath, internalPath) = FindArchivePath( absolutePath );

		if ( !string.IsNullOrEmpty( archivePath ) )
		{
			var archive = GetArchive( archivePath );
			return archive?.GetModifiedTime() ?? DateTime.UnixEpoch;
		}

		return new FileInfo( absolutePath ).LastWriteTime;
	}

	public string[] GetFiles( string relativePath )
	{
		return GetFileSystemEntries( relativePath, false );
	}

	public string[] GetDirectories( string relativePath )
	{
		return GetFileSystemEntries( relativePath, true );
	}

	private string[] GetFileSystemEntries( string relativePath, bool directories )
	{
		if ( overlays.Count == 0 ) return ListEntries( GetAbsolutePath( relativePath ), relativePath, directories );
		// Merge the listings of every root that has this folder; an overlay entry hides a base entry of the same name.
		var merged = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var root in Roots() )
		{
			var candidate = Combine( root, relativePath );
			if ( !Directory.Exists( candidate ) && string.IsNullOrEmpty( FindArchivePath( candidate ).ArchivePath ) ) continue;
			foreach ( var entry in ListEntries( candidate, relativePath, directories ) )
				merged.TryAdd( Path.GetFileName( entry.TrimEnd( '/', '\\' ) ), entry );
		}
		return merged.Values.ToArray();
	}

	private string[] ListEntries( string absolutePath, string relativePath, bool directories )
	{
		var (archivePath, internalPath) = FindArchivePath( absolutePath );

		if ( !string.IsNullOrEmpty( archivePath ) )
		{
			var archive = GetArchive( archivePath );
			var entries = directories ? archive.GetDirectories( internalPath ) : archive.GetFiles( internalPath );
			return entries.Select( entry => Path.Combine( relativePath, entry ) ).ToArray();
		}

		if ( directories )
		{
			var fileSystemDirectories = Directory.GetDirectories( absolutePath );
			var fileSystemArchives = Directory.GetFiles( absolutePath ).Where( x => archiveHandlers.Keys.Contains( Path.GetExtension( x ) ) ).Select( x => x[..x.LastIndexOf( "." )] );

			return fileSystemDirectories.Concat( fileSystemArchives ).ToArray();
		}
		else
		{
			return Directory.GetFiles( absolutePath ).Where( x => !archiveHandlers.Keys.Contains( Path.GetExtension( x ) ) ).ToArray();
		}
	}

	private IArchive GetArchive( string archivePath )
	{
		if ( archiveCache.TryGetValue( archivePath, out var archive ) )
		{
			return archive;
		}

		var extension = Path.GetExtension( archivePath );
		if ( archiveHandlers.TryGetValue( extension, out var handlerType ) )
		{
			archive = (IArchive)Activator.CreateInstance( handlerType, new[] { archivePath } );
			archiveCache[archivePath] = archive;
			return archive;
		}

		return null;
	}

	private (string ArchivePath, string InternalPath) FindArchivePath( string path )
	{
		var parts = path.Split( Path.DirectorySeparatorChar );
		var currentPath = new StringBuilder();

		foreach ( var part in parts )
		{
			if ( currentPath.Length > 0 )
			{
				currentPath.Append( Path.DirectorySeparatorChar );
			}

			currentPath.Append( part );

			foreach ( var handler in archiveHandlers )
			{
				var extension = handler.Key;
				var potentialArchivePath = $"{currentPath}{extension}";

				if ( archiveHandlers.ContainsKey( extension ) && File.Exists( potentialArchivePath ) )
				{
					var remainingPath = path.Substring( currentPath.Length );
					return (potentialArchivePath, remainingPath.TrimStart( Path.DirectorySeparatorChar ));
				}
			}

			if ( Directory.Exists( currentPath.ToString() ) )
			{
				continue;
			}
		}

		return (string.Empty, path);
	}

	/// <summary>The file or folder in the first root that has it (directly or inside an archive), else under the base path.</summary>
	public string GetAbsolutePath( string relativePath )
	{
		foreach ( var root in overlays )
		{
			var candidate = Combine( root, relativePath );
			if ( File.Exists( candidate ) || Directory.Exists( candidate ) || !string.IsNullOrEmpty( FindArchivePath( candidate ).ArchivePath ) )
				return candidate;
		}
		return Combine( basePath, relativePath );
	}

	public string GetRelativePath( string absolutePath )
	{
		var root = overlays.FirstOrDefault( overlay => absolutePath.StartsWith( overlay, StringComparison.OrdinalIgnoreCase ) ) ?? basePath;
		var path = Path.GetRelativePath( root, absolutePath ).Replace( "\\", "/" );
		return $"/{path}";
	}

	private IEnumerable<string> Roots() => overlays.Append( basePath );

	private static string Combine( string root, string relativePath ) => Path.Combine( root, relativePath.TrimStart( '/' ) ).Replace( "/", "\\" );

	public bool IsArchive( string path )
	{
		var (archivePath, internalPath) = FindArchivePath( path );

		return !string.IsNullOrEmpty( archivePath );
	}

	public FileSystemWatcher CreateWatcher( string relativeDir, string filter )
	{
		var directoryName = GetAbsolutePath( relativeDir );
		var watcher = new FileSystemWatcher( directoryName, filter );

		watcher.NotifyFilter = NotifyFilters.Attributes
							 | NotifyFilters.CreationTime
							 | NotifyFilters.DirectoryName
							 | NotifyFilters.FileName
							 | NotifyFilters.LastAccess
							 | NotifyFilters.LastWrite
							 | NotifyFilters.Security
							 | NotifyFilters.Size;

		watcher.EnableRaisingEvents = true;

		return watcher;
	}
}
