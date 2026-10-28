using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Linq;
using N = System.Numerics;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyConfigTests
{
	[TestMethod]
	public void RetailPlacementSeparatesModelRootsFromCameraAndFlockAnchors()
	{
		GameData.Require();
		var lobby = LobbyConfig.Load();
		var positions = new[] { new Vector3( 400, 400, 0 ), new Vector3( 600, 400, 0 ), new Vector3( 600, 600, 0 ), new Vector3( 400, 600, 0 ) };
		var heights = new[] { 12.5f, 17.5f, 38f, 35f };
		var rotatedX = new[] { -N.Vector3.UnitY, N.Vector3.UnitY, -N.Vector3.UnitX, N.Vector3.UnitX };
		foreach ( var island in lobby.Islands )
		{
			var center = lobby.CameraPositions[island.Id];
			var placement = LobbyIsland.GetPlacement( new Vector3( center.X, center.Y, 0 ), island );
			Assert.AreEqual( positions[island.Id], placement.Position, island.ThemeName );
			Assert.AreEqual( heights[island.Id], placement.Target.Z, island.ThemeName );
			Assert.AreEqual( placement.Position.X, placement.Target.X );
			Assert.AreEqual( placement.Position.Y, placement.Target.Y );
			Assert.IsTrue( N.Vector3.Distance( N.Vector3.Transform( N.Vector3.UnitX, placement.Rotation ), rotatedX[island.Id] ) < 0.00001f, island.ThemeName );
		}
	}

	[TestMethod]
	public void RetailThemeEnvironmentAndFlyingMeshesKeepTheirConfiguredValues()
	{
		GameData.Require();
		var islands = LobbyConfig.Load().Islands;
		var sky = new[] { new N.Vector3( 243, 203, 191 ), new N.Vector3( 5, 170, 255 ), new N.Vector3( 4, 44, 12 ), new N.Vector3( 125, 8, 8 ) };
		for ( int i = 0; i < 4; i++ )
		{
			Assert.AreEqual( i, islands[i].Id );
			Assert.AreEqual( sky[i], islands[i].SkyColour );
			Assert.AreEqual( i == 2, islands[i].Rainy );
			Assert.AreEqual( i == 2 ? 63 : 0, islands[i].Lightning );
		}
		CollectionAssert.AreEqual( new[] { 10, 10 }, islands[0].FlyingMeshes.Select( mesh => mesh.Count ).ToArray() );
		CollectionAssert.AreEqual( new[] { 1.5f, 1.5f }, islands[0].FlyingMeshes.Select( mesh => mesh.Speed ).ToArray() );
		Assert.IsTrue( islands[0].FlyingMeshes.All( mesh => mesh.Extents == new N.Vector3( 200, 100, 200 ) ) );
		Assert.AreEqual( 0, islands[1].FlyingMeshes.Count );
		Assert.AreEqual( 1, islands[2].FlyingMeshes.Count );
		Assert.AreEqual( 50, islands[2].FlyingMeshes[0].Count );
		Assert.AreEqual( 0, islands[3].FlyingMeshes.Count );
	}

	[TestMethod]
	public void RetailLobbyLoadsEveryIslandAndResource()
	{
		GameData.Require();
		var lobby = LobbyConfig.Load();
		Assert.AreEqual( 100f, lobby.IslandFovDegrees );
		Assert.AreEqual( 0.02f, lobby.SpinSpeed );
		Assert.AreEqual( 70f, lobby.SpinRadius );
		Assert.AreEqual( 20f, lobby.VerticalOffset );
		Assert.AreEqual( 475f, lobby.GlobeRadiusOut );
		Assert.AreEqual( 200f, lobby.GlobeRadiusIn );
		Assert.AreEqual( 10, lobby.CameraPositions.Count );
		Assert.AreEqual( 4, lobby.Islands.Count );
		CollectionAssert.AreEqual( new[] { "jungle", "fantasy", "hallow", "space" }, lobby.Islands.Select( x => x.ThemeName ).ToArray() );
		var expected = new[] { new N.Vector2( 400, 400 ), new N.Vector2( 600, 400 ), new N.Vector2( 600, 600 ), new N.Vector2( 400, 600 ) };
		var angles = new[] { 90f, 270f, 180f, 0f };
		var heights = new[] { 12.5f, 17.5f, 38f, 35f };
		foreach ( var island in lobby.Islands )
		{
			Assert.AreEqual( expected[island.Id], lobby.CameraPositions[island.Id] );
			Assert.AreEqual( angles[island.Id], island.RotationDegrees );
			Assert.AreEqual( heights[island.Id], island.Height );
			Assert.IsTrue( new ModelFile( island.ModelPath ).Meshes.Count > 0, island.ModelPath );
			Assert.IsTrue( new ModelFile( island.GateModelPath ).Meshes.Count > 0, island.GateModelPath );
			foreach ( var mesh in island.FlyingMeshes )
				Assert.IsTrue( new ModelFile( mesh.ModelPath ).Meshes.Count > 0, mesh.ModelPath );
		}
		Assert.AreEqual( "Lost Kingdom", lobby.Islands[0].Name );
		Assert.AreEqual( 2, lobby.Islands[0].FlyingMeshes.Count );
		Assert.AreEqual( 7, lobby.Islands[0].FlyingMeshes[0].CountAtQuality( 70 ) );
		Assert.AreEqual( 35, lobby.Islands[2].FlyingMeshes[0].CountAtQuality( 70 ) );
		Assert.AreEqual( 1, lobby.Islands[2].FlyingMeshes[0].CountAtQuality( 0 ) );
		Assert.AreEqual( new N.Vector3( 200, 100, 200 ), lobby.Islands[2].FlyingMeshes[0].Extents );
		Assert.AreEqual( 2.5f, lobby.Islands[2].FlyingMeshes[0].Speed );
		Assert.AreEqual( new N.Vector3( 4, 44, 12 ), lobby.Islands[2].SkyColour );
		Assert.AreEqual( 63, lobby.Islands[2].Lightning );
		Assert.IsTrue( lobby.Islands[2].Rainy );
		Assert.IsFalse( lobby.Islands[0].Rainy );
	}

	[TestMethod]
	public void FunctionSyntaxPreservesQuotedNamesAndUsesInvariantNumbers()
	{
		var previous = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo( "de-DE" );
			var lobby = LobbyConfig.Parse( "# comment\nislandcameraposition(4,12.5,20); // comment\nSPINSPEED(0.03)",
				new[] { ("custom", "SKYCOLOUR(1,2,3) ISLAND(4,\"data\\lobby\\terrain\",\"isle\",\"gate\",\"Name, with spaces\",90.5,15.25)") } );
			Assert.AreEqual( "Name, with spaces", lobby.Islands[0].Name );
			Assert.AreEqual( "lobby/terrain/isle.md2", lobby.Islands[0].ModelPath );
			Assert.AreEqual( 15.25f, lobby.Islands[0].Height );
			Assert.AreEqual( 0.03f, lobby.SpinSpeed );
			Assert.AreEqual( new N.Vector2( 12.5f, 20 ), lobby.CameraPositions[4] );
		}
		finally { CultureInfo.CurrentCulture = previous; }
	}

	[TestMethod]
	public void InvalidCommandsAndMissingCameraSlotsAreRejected()
	{
		var none = Array.Empty<(string, string)>();
		Assert.ThrowsException<FormatException>( () => LobbyConfig.Parse( "SPINRADIUS(70,80)", none ) );
		Assert.ThrowsException<FormatException>( () => LobbyConfig.Parse( "SPINSPEED(NaN)", none ) );
		Assert.ThrowsException<FormatException>( () => LobbyConfig.Parse( "SPINRADIUS(70", none ) );
		Assert.ThrowsException<FormatException>( () => LobbyConfig.Parse( "UNKNOWN(1)", none ) );
		Assert.ThrowsException<FormatException>( () => LobbyConfig.Parse( "", new[] { ("test", "ISLAND(0,\"a\",\"b\",\"c\",\"d\",0,0)") } ) );
	}
}
