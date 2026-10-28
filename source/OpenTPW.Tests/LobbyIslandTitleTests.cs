using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>Localized gate titles and the serialized sign-name fields used by the retail island page.</summary>
[TestClass]
public class LobbyIslandTitleTests
{
	[TestMethod]
	public void EveryIslandTitleUsesItsLocalizedGateLines()
	{
		GameData.Require();
		var config = LobbyConfig.Load();
		var navigation = new LobbyNavigation( config );
		navigation.Bind( PlayerProfileFile.Create( 0, new byte[39] ) );
		string[] expected = { "LOST Kingdom", "Wonder Land", "HALLOWEEN world", "Space Zone" };
		for ( int i = 0; i < config.Islands.Count; i++ )
		{
			Assert.AreEqual( expected[i], navigation.IslandTitle, navigation.Island.ThemeName );
			var lines = LobbySigns.LocalizedLines( navigation.Island.GateModelPath );
			Assert.AreEqual( LobbyObjectNames.Title( lines.First, lines.Second ), navigation.IslandTitle );
			navigation.Navigate( 1 );
		}
		Assert.ThrowsException<InvalidDataException>( () => LobbyObjectNames.Lines( 123 ) );
	}

	[TestMethod]
	public void SavedSignNamesRespectTheRenameFlagAndNativeJoinRules()
	{
		foreach ( var (first, second, expected) in new[] {
			("My", "Park", "My Park"), ("Won-", "derland", "Wonderland"),
			("", "Only", "Only"), ("Only", "", "Only"), ("", "", ""),
			("夢幻", "樂園", "夢幻 樂園"), (new string( 'A', 32 ), new string( 'B', 32 ), new string( 'A', 32 ) + " " + new string( 'B', 32 )) } )
		{
			var profile = SavedName( first, second, true );
			byte[] before = profile.Write();
			Assert.AreEqual( expected, LobbyPlayerProgress.IslandTitle( profile, "FANTASY", "Default" ) );
			CollectionAssert.AreEqual( before, profile.Write(), "Reading a title must preserve the player's data" );
			Assert.AreEqual( "Default", LobbyPlayerProgress.IslandTitle( profile, "jungle", "Default" ) );
		}
		Assert.AreEqual( "Default", LobbyPlayerProgress.IslandTitle( SavedName( "Old", "Name", false ), "fantasy", "Default" ) );
	}

	[TestMethod]
	public void BindingAnotherPlayerClearsThePreviousPlayersTitle()
	{
		GameData.Require();
		var navigation = new LobbyNavigation( LobbyConfig.Load() );
		navigation.Bind( SavedName( "My", "Fantasy", true ) );
		navigation.Navigate( 1 );
		Assert.AreEqual( "My Fantasy", navigation.IslandTitle );
		navigation.Unbind();
		Assert.AreEqual( "Wonder Land", navigation.IslandTitle );
		navigation.Bind( PlayerProfileFile.Create( 0, new byte[39] ) );
		navigation.Navigate( 1 );
		Assert.AreEqual( "Wonder Land", navigation.IslandTitle );
	}

	/// <summary>Version 12 profile with the native 6 bytes, four byte/dword pairs, interleaved name words and two flags.</summary>
	private static PlayerProfileFile SavedName( string first, string second, bool changed )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( 12u ); writer.Write( new byte[17] ); writer.Write( 1u );
		byte[] theme = Encoding.ASCII.GetBytes( "fantasy\0" );
		writer.Write( (uint)theme.Length ); writer.Write( theme );
		writer.Write( new byte[6] );
		for ( int i = 0; i < 4; i++ ) { writer.Write( (byte)0 ); writer.Write( 0u ); }
		for ( int i = 0; i < 33; i++ )
		{
			writer.Write( (ushort)(i < first.Length ? first[i] : '\0') );
			writer.Write( (ushort)(i < second.Length ? second[i] : '\0') );
		}
		writer.Write( changed ); writer.Write( false );
		writer.Write( new byte[39] ); writer.Write( 0u );
		return PlayerProfileFile.Read( stream.ToArray() );
	}
}
