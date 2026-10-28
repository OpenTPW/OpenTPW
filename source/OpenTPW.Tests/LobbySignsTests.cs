using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class LobbySignsTests
{
	[TestMethod]
	public void RetailLobbySignsDecodeBackgrounds()
	{
		GameData.Require();
		foreach ( string name in new[] { "Jun_isle", "Hal_isle", "Fan_gate", "Spa_gate" } )
		{
			var image = LobbySigns.DecodeBackground( FileSystem.ReadAllBytes( $"lobby/terrain/{name}.sgn" ) );
			Assert.IsTrue( image.Width >= 128 && image.Height >= 64, name );
			Assert.AreEqual( image.Width * image.Height * 4, image.Data.Length, name );
			bool varied = false;
			for ( int i = 4; i < image.Data.Length; i += 4 )
				if ( image.Data[i] != image.Data[0] ) { varied = true; break; }
			Assert.IsTrue( varied, name );
		}
	}

	[TestMethod]
	public void InvalidSignIsRejected()
	{
		Assert.ThrowsException<InvalidDataException>( () => LobbySigns.DecodeBackground( new byte[64] ) );
	}
	[TestMethod]
	public void RetailSignLayoutAndLocalizedLines()
	{
		GameData.Require();
		foreach ( string name in new[] { "Jun_isle", "Hal_isle", "Fan_gate", "Spa_gate" } )
		{
			var sign = LobbySigns.Decode( FileSystem.ReadAllBytes( $"lobby/terrain/{name}.sgn" ) );
			Assert.AreEqual( 2, sign.Lines.Length );
			Assert.AreEqual( 256, sign.Background.Width );
			Assert.AreEqual( 128, sign.Background.Height );
			foreach ( var line in sign.Lines )
			{
				Assert.IsTrue( line.Spacing > 0 );
				Assert.IsTrue( line.FontPath.EndsWith( ".ttf", System.StringComparison.OrdinalIgnoreCase ) || line.FontPath.EndsWith( ".ttc", System.StringComparison.OrdinalIgnoreCase ) );
				using var font = FileSystem.OpenRead( $"fonts/{line.FontPath}" );
				Assert.IsNotNull( font );
			}
			var text = LobbySigns.LocalizedLines( name );
			Assert.IsFalse( string.IsNullOrEmpty( text.First ) );
			// The shipped Traditional Chinese table stores zero-length second lines (entries
			// 86/180/273/369), so only the first localized line is expected to carry text.
		}
	}

	[TestMethod]
	public void SubstitutedSignFontIsRejected()
	{
		GameData.Require();
		// The first SGN line record starts after the 9-byte header and the two mode integers,
		// so its 64-byte face field is at 17. Renaming the face makes GDI fall back to another
		// installed font, which must not pass as the shipped sign font.
		byte[] data = FileSystem.ReadAllBytes( "lobby/terrain/Jun_isle.sgn" );
		System.Array.Clear( data, 17, 64 );
		System.Text.Encoding.ASCII.GetBytes( "NoSuchSignFace" ).CopyTo( data, 17 );
		var thrown = Assert.ThrowsException<InvalidDataException>( () => LobbySigns.Compose( data, "A", "" ) );
		StringAssert.Contains( thrown.Message, "NoSuchSignFace" );
	}

	[TestMethod]
	public void SignHalvesPreserveVisualTopFirstRows()
	{
		var bytes = new byte[256 * 128 * 4];
		bytes[0] = 11; bytes[128 * 4] = 22;
		bytes[127 * 256 * 4] = 33; bytes[(127 * 256 + 128) * 4] = 44;
		var image = new TextureData( 256, 128, bytes );
		var left = LobbySigns.Split( image, 0 );
		var right = LobbySigns.Split( image, 1 );
		Assert.AreEqual( (byte)11, left.Data[0] );
		Assert.AreEqual( (byte)22, right.Data[0] );
		Assert.AreEqual( (byte)33, left.Data[127 * 128 * 4] );
		Assert.AreEqual( (byte)44, right.Data[127 * 128 * 4] );
	}

	[TestMethod]
	public void ShippedFontsComposeBothLocalizedLines()
	{
		GameData.Require();
		foreach ( string name in new[] { "Jun_isle", "Hal_isle", "Fan_gate", "Spa_gate" } )
		{
			byte[] data = FileSystem.ReadAllBytes( $"lobby/terrain/{name}.sgn" );
			var text = LobbySigns.LocalizedLines( name );
			var image = LobbySigns.Compose( data, text.First, text.Second );
			var firstOnly = LobbySigns.Compose( data, text.First, "" );
			var secondOnly = LobbySigns.Compose( data, "", text.Second );
			var background = LobbySigns.DecodeBackground( data );
			int changes = 0, firstChanges = 0, secondChanges = 0;
			for ( int i = 0; i < image.Data.Length; i++ )
			{
				if ( image.Data[i] != background.Data[i] ) changes++;
				if ( image.Data[i] != secondOnly.Data[i] ) firstChanges++;
				if ( image.Data[i] != firstOnly.Data[i] ) secondChanges++;
			}
			Assert.IsTrue( changes > 100, name );
			Assert.IsTrue( firstChanges > 100, $"{name}: first localized line is missing" );
			// Signs whose second localized line is empty are shipped that way, so only assert the
			// draw difference for lines that actually have text.
			if ( text.Second.Length != 0 )
				Assert.IsTrue( secondChanges > 100, $"{name}: second localized line is missing" );
		}
	}

	private static byte[] FontFixture( byte[] text, int platform = 3, int encoding = 1 )
	{
		var data = new byte[46 + text.Length];
		void U16( int offset, int value ) { data[offset] = (byte)(value >> 8); data[offset + 1] = (byte)value; }
		void U32( int offset, int value ) { for ( int i = 0; i < 4; ++i ) data[offset + i] = (byte)(value >> (24 - i * 8)); }
		U32( 0, 0x10000 ); U16( 4, 1 );
		System.Text.Encoding.ASCII.GetBytes( "name" ).CopyTo( data, 12 );
		U32( 20, 28 ); U32( 24, 18 + text.Length );
		U16( 30, 1 ); U16( 32, 18 ); U16( 34, platform ); U16( 36, encoding ); U16( 40, 1 ); U16( 42, text.Length );
		text.CopyTo( data, 46 );
		return data;
	}

	[TestMethod]
	public void CollectionFacesAndUnknownRequestsCannotMatchAnotherFace()
	{
		var one = FontFixture( System.Text.Encoding.BigEndianUnicode.GetBytes( "FaceOne" ) );
		var two = FontFixture( System.Text.Encoding.BigEndianUnicode.GetBytes( "FaceTwo" ) );
		var ttc = new byte[20 + one.Length + two.Length];
		void U32( int offset, int value ) { for ( int i = 0; i < 4; ++i ) ttc[offset + i] = (byte)(value >> (24 - i * 8)); }
		System.Text.Encoding.ASCII.GetBytes( "ttcf" ).CopyTo( ttc, 0 ); U32( 4, 0x10000 ); U32( 8, 2 );
		U32( 12, 20 ); U32( 16, 20 + one.Length ); one.CopyTo( ttc, 20 ); two.CopyTo( ttc, 20 + one.Length );
		U32( 40, 48 ); U32( 40 + one.Length, 48 + one.Length );
		var faces = LobbySigns.FontNames.Read( ttc );
		Assert.AreEqual( 2, faces.Count );
		Assert.IsFalse( LobbySigns.NativeFont.FaceMatches( "FaceTwo", "FaceOne", faces ) );
		Assert.IsFalse( LobbySigns.NativeFont.FaceMatches( "FaceOne", "NoSuchSignFace", faces ) );
		Assert.IsTrue( LobbySigns.NativeFont.FaceMatches( "faceone", "FaceOne", faces ) );
	}

	[TestMethod]
	public void FontNamesDecodeMacRomanAndRejectInvalidLengths()
	{
		var faces = LobbySigns.FontNames.Read( FontFixture( new byte[] { 0x80 }, 1, 0 ) );
		Assert.IsTrue( faces[0].Contains( "Ä" ) );
		Assert.AreEqual( 0, LobbySigns.FontNames.Read( FontFixture( new byte[] { 0x80 }, 1, 1 ) ).Count );
		Assert.ThrowsException<InvalidDataException>( () => LobbySigns.FontNames.Read( FontFixture( new byte[] { 0x41 } ) ) );
		var truncated = new byte[16]; System.Text.Encoding.ASCII.GetBytes( "ttcf" ).CopyTo( truncated, 0 );
		truncated[8] = 0x40; truncated[11] = 1;
		Assert.ThrowsException<InvalidDataException>( () => LobbySigns.FontNames.Read( truncated ) );
		Assert.IsTrue( LobbySigns.NativeFont.FaceMatches( "Exact", "Exact", LobbySigns.FontNames.Read( new byte[0] ) ) );
		Assert.IsFalse( LobbySigns.NativeFont.FaceMatches( "Other", "Exact", LobbySigns.FontNames.Read( new byte[0] ) ) );
	}

	[TestMethod]
	public void NonEmptySecondLineStillContributesToTheSign()
	{
		GameData.Require();
		byte[] data = FileSystem.ReadAllBytes( "lobby/terrain/Jun_isle.sgn" );
		var both = LobbySigns.Compose( data, "第一行", "第二行" );
		var first = LobbySigns.Compose( data, "第一行", "" );
		int changed = 0;
		for ( int i = 0; i < both.Data.Length; ++i ) if ( both.Data[i] != first.Data[i] ) ++changed;
		Assert.IsTrue( changed > 100 );
	}

}
