using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class FileSystemOverlayTests
{
	[TestMethod]
	public void OverlayFilesReplaceBaseFilesAndListingsMerge()
	{
		string root = Path.Combine( Path.GetTempPath(), "opentpw-overlay-" + Guid.NewGuid().ToString( "N" ) );
		try
		{
			string game = Path.Combine( root, "game", "data" ), pack = Path.Combine( root, "pack", "data" );
			Directory.CreateDirectory( Path.Combine( game, "Language", "English" ) );
			Directory.CreateDirectory( Path.Combine( pack, "Language", "German" ) );
			File.WriteAllText( Path.Combine( game, "Language", "English", "UITEXT.str" ), "base" );
			File.WriteAllText( Path.Combine( game, "shared.txt" ), "base" );
			File.WriteAllText( Path.Combine( pack, "shared.txt" ), "pack" );
			File.WriteAllText( Path.Combine( pack, "Language", "German", "UITEXT.str" ), "german" );

			var files = new BaseFileSystem( game );
			files.AddOverlay( pack );

			Assert.AreEqual( "pack", files.ReadAllText( "shared.txt" ) );
			Assert.AreEqual( "base", files.ReadAllText( "Language/English/UITEXT.str" ) );
			Assert.AreEqual( "german", files.ReadAllText( "Language/German/UITEXT.str" ) );
			CollectionAssert.AreEquivalent( new[] { "English", "German" }, GameLanguage.Available( files ) );
			Assert.AreEqual( 1, files.GetFiles( "" ).Count( file => Path.GetFileName( file ) == "shared.txt" ) );
			Assert.AreEqual( pack, GameLanguage.PackDataFolder( Path.Combine( root, "pack" ) ) );
			CollectionAssert.AreEqual( new[] { "German" }, GameLanguage.PackLanguages( pack ) );
		}
		finally
		{
			Directory.Delete( root, true );
		}
	}
}
