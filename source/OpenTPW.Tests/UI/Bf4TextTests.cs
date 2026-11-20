using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;

namespace OpenTPW.Tests.UI;

/// <summary>Shared resource-index and layout rules against World language data.</summary>
[TestClass]
public sealed class Bf4TextTests
{
	[TestMethod]
	public void ReadsInstalledWorldLanguageIndicesAndFontSlots()
	{
		GameData.Require();
		foreach ( string language in GameLanguage.Available( FileSystem ) )
		{
			Stream Open( string name ) => FileSystem.OpenRead( $"Language/{language}/{name}" );
			using var stream = Open( "residx.dat" );
			var index = new ResourceIndexFile( stream );
			Assert.IsTrue( index.Entries.Count > 0 );
			foreach ( var entry in index.Entries.Values )
				using ( var resource = Open( entry.Name ) ) Assert.IsTrue( resource.Length > 0, entry.Name );
			var slots = Enumerable.Range( 0, 13 ).Select( slot => index.Entries.Values.Single(
				entry => entry.Name.Equals( LobbyFonts.Name( slot ) + ".bf4", StringComparison.OrdinalIgnoreCase ) ).Id );
			var fonts = new Bf4FontSet( index, slots, Open );
			Assert.AreEqual( 13, fonts.Count );
			Assert.AreSame( fonts[6], fonts[9], "World's repeated GAME7 slots must share the loaded font." );
			CollectionAssert.AreEqual( new[] { "Theme", "Park" }, fonts.Layout( 6, "Theme\nPark", 1000 ) );
		}
		using var fontStream = FileSystem.OpenRead( GameLanguage.Path( "GAME7.bf4" ) );
		var font = new FontFile( fontStream );
		CollectionAssert.AreEqual( new[] { "Theme", "Park" }, Bf4TextLayout.Layout( "Theme\nPark", 1000, s => font.MeasureString( s ) ) );
	}

	[TestMethod]
	public void ResourceIndexRejectsTruncationAndInvalidOffsets()
	{
		byte[] bytes = [ 66, 70, 82, 73, 1, 0, 0, 0, 1, 0, 0, 0, 4, 0, 12, 0, 0, 0, 0, 0, 65, 0 ];
		var index = new ResourceIndexFile( new MemoryStream( bytes ) );
		Assert.AreEqual( "A", index[1].Name );
		Assert.AreEqual( (ushort)4, index[1].Type );
		for ( int length = 0; length < bytes.Length; length++ )
			Assert.ThrowsExactly<InvalidDataException>( () => new ResourceIndexFile( new MemoryStream( bytes, 0, length ) ) );
		bytes[14] = 0;
		Assert.ThrowsExactly<InvalidDataException>( () => new ResourceIndexFile( new MemoryStream( bytes ) ) );
	}
}
