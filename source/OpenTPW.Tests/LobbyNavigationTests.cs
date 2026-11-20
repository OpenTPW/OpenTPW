using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyNavigationTests
{
	[TestMethod]
	public void PlayerBindingNavigationAndKeyGatesUseTheShippedThemes()
	{
		GameData.Require();
		var config = LobbyConfig.Load();
		var navigation = new LobbyNavigation( config );
		var selected = new List<int>();
		navigation.IslandSelected += selected.Add;
		int starts = 0, cancels = 0;
		navigation.EntryRequested += () => starts++;
		navigation.EntryCancelled += () => cancels++;
		Assert.IsFalse( navigation.Enter() );
		// Full Simulation (creation mode 0) uses the key gates; Instant Action is covered separately.
		var profile = PlayerProfileFile.Create( 0, PlayerProfileFile.DefaultPreferences( new SettingsFile( "sound.sam" ) ) );
		navigation.Bind( profile );
		Assert.IsFalse( navigation.InstantAction );
		Assert.AreEqual( config.Islands[0].Id, selected.Last() );
		Assert.IsFalse( navigation.Enter() );
		LobbyPlayerProgress.GrantIntroductionKey( profile );
		Assert.AreEqual( 1, navigation.RequiredKeys );
		Assert.IsTrue( navigation.Enter() );
		Assert.IsFalse( navigation.Enter() );
		navigation.Navigate( 1 );
		Assert.AreEqual( config.Islands[0].Id, navigation.Island.Id );
		Assert.IsTrue( navigation.CancelEntry() );
		Assert.IsFalse( navigation.CancelEntry() );
		Assert.AreEqual( 1, starts ); Assert.AreEqual( 1, cancels );
		for ( int i = 1; i <= config.Islands.Count; i++ )
		{
			navigation.Navigate( 1 );
			Assert.AreEqual( config.Islands[i % config.Islands.Count].Id, selected.Last() );
			Assert.AreEqual( navigation.Keys >= navigation.RequiredKeys, navigation.CanEnter );
		}
		navigation.Navigate( -1 );
		Assert.AreEqual( config.Islands[^1].Id, navigation.Island.Id );
		navigation.Unbind(); navigation.Navigate( 1 );
		Assert.IsFalse( navigation.Active ); Assert.IsFalse( navigation.Enter() );
	}
	[TestMethod]
	public void InstantActionFixesTheIslandAndSkipsKeyGates()
	{
		GameData.Require();
		var config = LobbyConfig.Load();
		var navigation = new LobbyNavigation( config );
		var selected = new List<int>();
		navigation.IslandSelected += selected.Add;
		var profile = PlayerProfileFile.Create( 1, PlayerProfileFile.DefaultPreferences( new SettingsFile( "sound.sam" ) ) );
		navigation.Bind( profile );
		Assert.IsTrue( navigation.InstantAction );
		Assert.AreEqual( 0, navigation.Keys );
		navigation.Navigate( 1 ); navigation.Navigate( -1 );
		Assert.AreEqual( config.Islands[0].Id, navigation.Island.Id );
		Assert.AreEqual( 1, selected.Count );
		Assert.IsTrue( navigation.CanEnter );
		Assert.IsTrue( navigation.Enter() );
		Assert.IsTrue( navigation.CancelEntry() );
	}
	[TestMethod]
	public void NativeIslandControlsResolveAllStatesAndTextures()
	{
		GameData.Require();
		foreach ( var (hash, states) in new (uint, int)[] {
			(0xa1f674e6,1), (0x9044a4cc,6), (0x64b12618,6), (0x795da8e1,6), (0xf1d0542a,6), (0x2f575715,2), (0x21aea625,10), (0x1d50cb14,4) } )
		{
			string path = LobbyControlResources.ResolveModel( hash );
			byte[] bytes = FileSystem.ReadAllBytes( path );
			for ( int state = 0; state < states; state++ )
			{
				var geometry = new LobbyButtonGeometry( bytes, state );
				Assert.IsTrue( geometry.VisibleMeshes.Length > 0, $"{path}:{state}" );
				foreach ( var material in geometry.VisibleMeshes.SelectMany( index => geometry.Model.Meshes[index].Materials ) )
					Assert.IsTrue( new TextureFile( AnimationFiles.ResolveFile( $"ui/textures/{material.Name}.wct", "Island control texture" ) ).Data.Width > 0 );
			}
		}
		foreach ( string font in new[] { "MENUMED", "GAME6AA" } ) Assert.IsTrue( new FontFile( GameLanguage.Path( $"{font}.bf4" ) ).Glyphs.Length > 0 );
	}
}
