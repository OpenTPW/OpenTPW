global using static OpenTPW.Common.GlobalNamespace;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class FileSystemTests
{
	[TestInitialize]
	public void Init()
	{
		GameData.Require();
	}

	[TestMethod]
	public void TestRead()
	{
		Assert.IsTrue( FileSystem.ReadAllText( "Challenges.sam" ).Length > 0 );
	}

	[TestMethod]
	public void TestReadArchive()
	{
		Assert.IsTrue( FileSystem.ReadAllText( "levels/jungle/terrain/qickload.txt" ).Length > 0 );
	}

	[TestMethod]
	public void EnumerateFiles()
	{
		var files = FileSystem.GetFiles( "/levels" );
		var directories = FileSystem.GetDirectories( "/levels" );

		foreach ( var directory in directories )
		{
			Console.WriteLine( $"{directory}/" );
		}

		foreach ( var file in files )
		{
			Console.WriteLine( $"{file}" );
		}

		Assert.IsTrue( files.Length > 0 );
		Assert.IsTrue( directories.Length > 0 );
	}

	[TestMethod]
	public void EnumerateFilesWADArchive()
	{
		var files = FileSystem.GetFiles( "/fonts" );

		foreach ( var item in files )
		{
			Console.WriteLine( $"{item}" );
		}

		Assert.IsTrue( files.Length > 0 );
		Assert.IsTrue( files.Any( x => x.EndsWith( "TTF" ) ) );
	}

	[TestMethod]
	public void LoadFromArchiveDirectory()
	{
		var file = FileSystem.ReadAllBytes( "/levels/jungle/terrain/textures/jgr_bas1.wct" );

		Assert.IsTrue( file.Length > 0 );
	}

	[TestMethod]
	public void EnumerateFilesSDTArchive()
	{
		var files = FileSystem.GetFiles( "/global/sound/AmbientHD" );

		foreach ( var item in files )
		{
			Console.WriteLine( $"{item}" );
		}

		Assert.IsTrue( files.Length > 0 );
		Assert.IsTrue( files.Any( x => x.EndsWith( "mp2" ) ) );
	}
}
