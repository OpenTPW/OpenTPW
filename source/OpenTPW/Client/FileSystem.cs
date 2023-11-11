﻿using System.Text;

namespace OpenTPW;

public class FileSystem
{
	public static FileSystem Game { get; } = new FileSystem( Settings.Default.GamePath );
	private string BasePath { get; }

	private Dictionary<string, WadArchive> WadArchiveCache { get; } = new();
	private Dictionary<string, SDTArchive> SdtArchiveCache { get; } = new();

	public FileSystem( string relativePath )
	{
		BasePath = Path.GetFullPath( relativePath, Directory.GetCurrentDirectory() );
	}

	public string GetAbsolutePath( string relativePath, bool ignorePathNotFound = false )
	{
		var path = Path.Combine( this.BasePath, relativePath );

		if ( !File.Exists( path ) && !Directory.Exists( path ) && !ignorePathNotFound && !IsWad( path ) )
			Log.Warning( $"Path not found: {path}. Continuing anyway." );

		return path;
	}

	public string ReadAllText( string relativePath )
	{
		return Encoding.ASCII.GetString( ReadAllBytes( relativePath ) );
	}

	public byte[] ReadAllBytes( string relativePath )
	{
		var stream = InternalOpenFile( relativePath );
		var bytes = new byte[stream.Length];
		stream.Read( bytes, 0, (int)stream.Length );

		return bytes;
	}

	public Stream OpenRead( string relativePath )
	{
		return InternalOpenFile( GetAbsolutePath( relativePath ) );
	}

	public string[] GetDirectories( string relativePath )
	{
		var dirs = InternalGetDirectories( GetAbsolutePath( relativePath ) );
		var archives = InternalGetFiles( GetAbsolutePath( relativePath ) ).Where( x => x.EndsWith( ".wad" ) || x.EndsWith( ".sdt" ));
		
		if( dirs == null )
		{
			return archives.ToArray();
		}

		return dirs.Concat( archives ).ToArray();
	}

	public string[] GetFiles( string relativePath )
	{
		var dirs = InternalGetFiles( GetAbsolutePath( relativePath ) );

		return dirs.Where( x => !x.EndsWith( ".wad" ) ).ToArray();
	}

	/// <summary>
	/// Retrieves a WAD archive from, or adds a WAD archive to, the archive cache.
	/// </summary>
	private WadArchive GetArchive( string relativePath )
	{
		if ( !WadArchiveCache.TryGetValue( relativePath, out var archive ) )
		{
			archive = new WadArchive( GetAbsolutePath( relativePath ) );
			WadArchiveCache[relativePath] = archive;
		}

		return archive;
	}

	private SDTArchive GetSdtArchive( string relativePath )
	{
		if( !SdtArchiveCache.TryGetValue( relativePath, out var archive ) )
		{
			archive = new SDTArchive( GetAbsolutePath( relativePath ) );
			SdtArchiveCache[relativePath] = archive;
		}

		return archive;
	}

	/// <summary>
	/// Checks if a path is located within (or points to) a WAD
	/// </summary>
	private bool IsWad( string relativePath )
	{
		return relativePath.Contains( ".wad" );
	}

	/// <summary>
	/// Checks if a path is located within (or points to) a SDT
	/// </summary>
	private bool IsSdt( string relativePath )
	{
		return relativePath.Contains( ".sdt" );
	}

	/// <summary>
	/// Gets the archive path and internal path for a particular path
	/// </summary>
	private (string WadPath, string InternalPath) DissectPath( string relativePath )
	{
		if ( !relativePath.Contains( ".wad" ) )
			return ("", "");

		// Find archive in path
		var archivePath = relativePath[..(relativePath.IndexOf( ".wad" ) + 4)];

		if ( relativePath.EndsWith( ".wad" ) )
			return (archivePath, "");

		// Find file in path
		var internalPath = relativePath[(relativePath.IndexOf( ".wad" ) + 5)..];

		return (archivePath, internalPath);
	}

	private (string SdtPath, string InternalPath) DissectSdtPath( string relativePath )
	{
		if ( !relativePath.Contains( ".sdt" ) )
			return ("", "");

		// Find archive in path
		var archivePath = relativePath[..(relativePath.IndexOf( ".wad" ) + 4)];

		if ( relativePath.EndsWith( ".sdt" ) )
			return (archivePath, "");

		// Find file in path
		var internalPath = relativePath[(relativePath.IndexOf( ".wad" ) + 5)..];

		return (archivePath, internalPath);
	}

	/// <summary>
	/// Handles enumerating through directories based on whether they're part of a WAD or not
	/// </summary>
	private string[] InternalGetDirectories( string relativePath )
	{
		if ( IsWad( relativePath ) )
		{
			var (archivePath, internalPath) = DissectPath( relativePath );
			var archive = GetArchive( archivePath );

			return archive.GetDirectories( internalPath ).Select( x => relativePath + "\\" + x ).ToArray();
		}
		else if ( IsSdt (relativePath) )
		{
			return new string[0];
		}
		else
		{
			return Directory.GetDirectories( relativePath );
		}
	}

	/// <summary>
	/// Handles enumerating through files based on whether they're part of a WAD or not
	/// </summary>
	private string[] InternalGetFiles( string relativePath )
	{
		if ( IsWad( relativePath ) )
		{
			var (archivePath, internalPath) = DissectPath( relativePath );
			var archive = GetArchive( archivePath );

			return archive.GetFiles( internalPath ).Select( x => relativePath + "\\" + x ).ToArray();
		}
		else if ( IsSdt( relativePath ) )
		{ 
			var archive = GetSdtArchive( relativePath );
			return archive.GetFiles( relativePath ).Select( x => relativePath + "\\" + x ).ToArray();
	
		}
		else
		{
			return Directory.GetFiles( relativePath );
		}
	}

	/// <summary>
	/// Handles opening a file based on whether it's part of a WAD or not
	/// </summary>
	private Stream InternalOpenFile( string relativePath )
	{
		if ( IsWad( relativePath ) )
		{
			var (archivePath, internalPath) = DissectPath( relativePath );
			var archive = GetArchive( archivePath );

			var file = archive.GetFile( internalPath );

			return new MemoryStream( file.Data );
		}
		else if ( relativePath.Contains(".m" ) )
		{
			int lastSlash = relativePath.LastIndexOf( '\\' );
			string internalPath = relativePath.Substring( 0, lastSlash );
			string name = relativePath.Substring( relativePath.LastIndexOf( '\\' ) + (relativePath.Length - lastSlash) );
			
			var archive = GetSdtArchive( internalPath );

			
			var file = archive.GetFile( name );
			
			return new MemoryStream( file.Data );
		}
		else
		{
			return File.OpenRead( relativePath );
		}
	}
}
