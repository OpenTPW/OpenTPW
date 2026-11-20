using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class TctTests
{
	[TestMethod]
	public void ParsesSectionsRowsAndComments()
	{
		var table = new TctFile( "#\tcomment\r\n3 orphan.tga\nPathTex\n0\tjpa_squ1.tga\n2 textures\\jpa_str1.TGA\n\nOther\n1 x.tga\nPathTex\n1 jpa_end1.tga\n" );
		CollectionAssert.AreEqual( new[] { "jpa_squ1", "jpa_end1", "jpa_str1" }, table["PathTex"].ToArray() );
		CollectionAssert.AreEqual( new[] { "", "x" }, table["Other"].ToArray() );
		Assert.AreEqual( 0, table["Missing"].Count );
	}

	[TestMethod]
	public void EveryLevelHasPathTextures()
	{
		GameData.Require();

		var levels = FileSystem.GetDirectories( "levels" ).Select( x => System.IO.Path.GetFileName( x ) )
			.Where( x => FileSystem.GetDirectories( $"levels/{x}" ).Any( d => System.IO.Path.GetFileName( d ).Equals( "terrain", StringComparison.OrdinalIgnoreCase ) ) ).ToList();
		Assert.IsTrue( levels.Count > 0 );

		foreach ( var level in levels )
		{
			var path = FileSystem.GetFiles( $"levels/{level}/terrain" ).Single( x => x.EndsWith( ".tct", StringComparison.OrdinalIgnoreCase ) );
			var table = new TctFile( FileSystem.OpenRead( path ) );
			Assert.IsTrue( table["PathTex"].Count > 0, level );
			Assert.IsTrue( table["PathTex"].All( x => x.Length > 0 ), level );
		}
	}
}
