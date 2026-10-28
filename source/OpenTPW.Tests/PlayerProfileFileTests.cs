using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class PlayerProfileFileTests
{
	[TestMethod]
	public void NativeConstructorDefaultsProduceCompleteEmptyProfile()
	{
		GameData.Require();
		byte[] preferences = PlayerProfileFile.DefaultPreferences( new SettingsFile( "sound.sam" ) );
		CollectionAssert.AreEqual( new uint[] { 75, 60, 75, 100 }, Enumerable.Range( 0, 4 ).Select( i => BitConverter.ToUInt32( preferences, i * 8 + 4 ) ).ToArray() );
		var profile = PlayerProfileFile.Create( 1, preferences );
		byte[] bytes = profile.Write();
		Assert.AreEqual( 68, bytes.Length );
		Assert.AreEqual( 1, bytes[18] );
		Assert.AreEqual( 1, bytes[19] );
		Assert.AreEqual( 1, bytes[20] );
		CollectionAssert.AreEqual( bytes, PlayerProfileFile.Read( bytes ).Write() );
		foreach ( int length in new[] { 0, 4, 21, 25, 63, 67 } )
			Assert.ThrowsException<InvalidDataException>( () => PlayerProfileFile.Read( bytes[..length] ) );
		Assert.ThrowsException<InvalidDataException>( () => PlayerProfileFile.Read( bytes.Concat( new byte[1] ).ToArray() ) );
	}

	[TestMethod]
	public void VariableSegmentsPreferencesAndRideIdsRoundTripWithoutDroppingUnknownBytes()
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( 12u );
		writer.Write( Enumerable.Range( 0, 17 ).Select( i => (byte)i ).ToArray() );
		writer.Write( 2u );
		foreach ( string name in new[] { "jungle", "space" } )
		{
			writer.Write( (uint)name.Length );
			writer.Write( System.Text.Encoding.ASCII.GetBytes( name ) );
			writer.Write( Enumerable.Range( 0, 160 ).Select( i => (byte)i ).ToArray() );
		}
		writer.Write( Enumerable.Range( 0, 39 ).Select( i => (byte)(255 - i) ).ToArray() );
		writer.Write( 2u ); writer.Write( (ushort)1 ); writer.Write( (ushort)65535 );
		byte[] bytes = stream.ToArray();
		var profile = PlayerProfileFile.Read( bytes );
		Assert.AreEqual( "space", profile.Segments[1].Name );
		Assert.AreEqual( 5 + BitConverter.ToInt32( profile.Prefix, 10 ), LobbyPlayerProgress.KeyCount( profile, new[] { "jungle", "space" } ) );
		Assert.AreEqual( 3 + BitConverter.ToInt32( profile.Prefix, 10 ), LobbyPlayerProgress.KeyCount( profile, new[] { "JUNGLE" } ) );
		CollectionAssert.AreEqual( bytes, profile.Write() );
		BitConverter.GetBytes( uint.MaxValue ).CopyTo( bytes, 21 );
		Assert.ThrowsException<InvalidDataException>( () => PlayerProfileFile.Read( bytes ) );
	}

	[TestMethod]
	public void CreationCopiesOnlyNativeTemplatesAndNeverOverwritesAnOccupiedSlot()
	{
		GameData.Require();
		string directory = Path.Combine( Path.GetTempPath(), "opentpw-profile-" + Guid.NewGuid().ToString( "N" ) );
		var store = new LobbyPlayerStore( directory, FileSystem, new[] { "jungle", "fantasy", "hallow", "space" } );
		try
		{
			var profile = store.Create( 2, "Test", 1 );
			Assert.AreEqual( 2, store.ReadSlots()[2]!.Slot );
			Assert.AreEqual( 1, store.Load( profile ).CreationMode );
			Assert.AreEqual( 1, LobbyPlayerProgress.KeyCount( store.Load( profile ), new[] { "jungle", "fantasy", "hallow", "space" } ) );
			Assert.IsTrue( File.Exists( Path.Combine( profile.Directory, "jungle", "easymode.TPWI" ) ) );
			CollectionAssert.AreEqual( FileSystem.ReadAllBytes( "levels/jungle/easymode.TPWI" ), File.ReadAllBytes( Path.Combine( profile.Directory, "jungle", "easymode.TPWI" ) ) );
			Assert.ThrowsException<IOException>( () => store.Create( 2, "Other", 0 ) );
			Assert.AreEqual( 1, LobbyPlayerProgress.KeyCount( store.Load( profile ), new[] { "jungle" } ), "An occupied-slot retry must not award another introduction key" );
			Assert.ThrowsException<ArgumentException>( () => store.Create( 1, "../Invalid", 0 ) );
			Assert.AreEqual( 1, Directory.GetDirectories( directory ).Length );
		}
		finally { if ( Directory.Exists( directory ) ) Directory.Delete( directory, true ); }
	}
}
