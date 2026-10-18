using System.Text;

namespace OpenTPW;

public class BaseFileSystem
{
	private readonly string basePath;
	private readonly Dictionary<string, Type> archiveHandlers = new();
	private readonly Dictionary<string, IArchive> archiveCache = new();

	public BaseFileSystem( string relativePath )
	{
		if ( !Directory.Exists( relativePath ) )
		{
			Directory.CreateDirectory( relativePath );
		}

		basePath = Path.GetFullPath( relativePath, Directory.GetCurrentDirectory() );
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
		var (archivePath, internalPath) = FindArchivePath( absolutePath );
		if ( !string.IsNullOrEmpty( archivePath ) )
		{
			return GetArchive( archivePath )?.GetFile( internalPath ) != null;
		}

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
		{
			Directory.CreateDirectory( directoryName );
		}

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
		var absolutePath = GetAbsolutePath( relativePath );
		var (archivePath, internalPath) = FindArchivePath( absolutePath );
		if ( !string.IsNullOrEmpty( archivePath ) )
		{
			var archive = GetArchive( archivePath );
			var entries = directories ? archive.GetDirectories( internalPath ) : archive.GetFiles( internalPath );
			return entries.Select( entry => Path.Combine( relativePath, entry ).Replace( "\\", "/" ) ).ToArray();
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
		var parts = path.Split( "/" );
		var currentPath = new StringBuilder();
		foreach ( var part in parts )
		{
			if ( currentPath.Length > 0 )
			{
				currentPath.Append( '/' );
			}

			currentPath.Append( part );
			foreach ( var handler in archiveHandlers )
			{
				var extension = handler.Key;
				var potentialArchivePath = $"{currentPath}{extension}";
				if ( archiveHandlers.ContainsKey( extension ) && File.Exists( potentialArchivePath ) )
				{
					var remainingPath = path.Substring( currentPath.Length );
					return (potentialArchivePath, remainingPath.TrimStart( '/' ));
				}
			}

			if ( Directory.Exists( currentPath.ToString() ) )
			{
				continue;
			}
		}

		return (string.Empty, path);
	}

	public string GetAbsolutePath( string relativePath )
	{
		return Path.Combine( basePath, relativePath.TrimStart( '/' ) ).Replace( "\\", "/" );
	}

	public string GetRelativePath( string absolutePath )
	{
		var path = Path.GetRelativePath( basePath, absolutePath ).Replace( "\\", "/" );
		return $"/{path}";
	}

	public bool IsArchive( string path )
	{
		var (archivePath, internalPath) = FindArchivePath( path );
		return !string.IsNullOrEmpty( archivePath );
	}

	public FileSystemWatcher CreateWatcher( string relativeDir, string filter )
	{
		var directoryName = GetAbsolutePath( relativeDir );
		var watcher = new FileSystemWatcher( directoryName, filter );
		watcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.Security | NotifyFilters.Size;
		watcher.EnableRaisingEvents = true;
		return watcher;
	}
}
