using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Tests over the whole game data. Set OPENTPW_GAME_PATH to the game folder (the one with data/);
/// without it, or without the data, these tests are inconclusive rather than failing.
/// </summary>
internal static class GameData
{
	public static string GamePath => Environment.GetEnvironmentVariable( "OPENTPW_GAME_PATH" ) ?? "C:\\Program Files (x86)\\Bullfrog\\Theme Park World";

	public static void Require()
	{
		var data = Path.Combine( GamePath, "data" );
		if ( !Directory.Exists( data ) )
			Assert.Inconclusive( $"No game data at {data}; set OPENTPW_GAME_PATH" );

		Log ??= new();
		FileSystem = new BaseFileSystem( data );
		FileSystem.RegisterArchiveHandler<WadArchive>( ".wad" );
		FileSystem.RegisterArchiveHandler<SdtArchive>( ".sdt" );
	}

	/// <summary>
	/// Every file with the extension, inside archives too.
	/// </summary>
	public static List<string> FindFiles( string extension )
	{
		var found = new List<string>();
		var pending = new Stack<string>();
		pending.Push( "/" );

		while ( pending.Count > 0 )
		{
			var directory = pending.Pop();
			found.AddRange( FileSystem.GetFiles( directory ).Where( x => x.EndsWith( extension, StringComparison.OrdinalIgnoreCase ) ) );

			foreach ( var child in FileSystem.GetDirectories( directory ) )
				pending.Push( child );
		}

		return found;
	}
}
