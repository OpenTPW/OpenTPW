using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class StringAndSaveTests
{
	[TestMethod]
	public void EveryStringTableReads()
	{
		GameData.Require();

		var tables = FileSystem.GetFiles( "Language/English" ).Where( x => x.EndsWith( ".str", StringComparison.OrdinalIgnoreCase ) ).ToList();
		Assert.IsTrue( tables.Count > 0 );

		var failures = new List<string>();
		foreach ( var path in tables )
		{
			try
			{
				var bytes = File.ReadAllBytes( path );
				var file = new StringFile( new MemoryStream( bytes ) );
				if ( file.Entries.Length != BitConverter.ToInt32( bytes, 8 ) )
					failures.Add( $"{path}: {file.Entries.Length} entries" );
			}
			catch ( Exception e )
			{
				failures.Add( $"{path}: {e.Message}" );
			}
		}

		Assert.AreEqual( 0, failures.Count, string.Join( "\n", failures ) );
	}

	[TestMethod]
	public void EverySaveInTheDataDecompresses()
	{
		GameData.Require();

		var saves = GameData.FindFiles( ".tpws" ).Concat( GameData.FindFiles( ".tpwi" ) ).ToList();
		if ( saves.Count == 0 )
			Assert.Inconclusive( "No saves in the game data" );

		foreach ( var path in saves )
			Assert.IsTrue( new SaveReader( FileSystem.OpenRead( path ) ).ReadFile().Length > 0, path );
	}
}
