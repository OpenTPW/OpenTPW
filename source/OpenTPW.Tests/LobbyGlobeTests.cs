using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using N = System.Numerics;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyGlobeTests
{
	[TestMethod]
	public void GlobeCameraUsesNativeRatesZoomAndPitchLimits()
	{
		GameData.Require();
		var config = LobbyConfig.Load();
		var camera = new LobbyGlobeState( config );
		Assert.AreEqual( config.GlobeRadiusOut, camera.Distance );
		camera.Turn( 1, 1 ); camera.Update( 1, new N.Vector2( 0.5f ) );
		Assert.AreEqual( 0.075f, camera.Yaw, 0.000001 );
		Assert.AreEqual( 0.075f, camera.Pitch, 0.000001 );
		Assert.AreEqual( camera.Distance, camera.Position.Length(), 0.001 );
		camera.ToggleZoom(); camera.Update( 1, new N.Vector2( 0.5f ) );
		Assert.AreEqual( config.GlobeRadiusOut + (config.GlobeRadiusIn - config.GlobeRadiusOut) * 0.3f, camera.Distance, 0.001 );
		for ( int i = 0; i < 100; i++ ) { camera.Turn( 0, 1 ); camera.Update( 1, new N.Vector2( 0.5f ) ); }
		Assert.AreEqual( 1.5f, camera.Pitch );
		camera.Reset(); camera.Update( 1, new N.Vector2( 0, 1 ) );
		Assert.IsTrue( camera.Yaw < 0 && camera.Pitch < 0 );
		camera.Reset(); Assert.AreEqual( 0f, camera.Yaw ); Assert.IsFalse( camera.Zoomed );
	}
	[TestMethod]
	public void PlacePickingPreservesTheSelectedObjectAndAppliesEachPagePredicate()
	{
		var a = new LobbyGlobePlace( "North Pole", new N.Vector3( 116, 0, 0 ), 1, 1, 5 );
		var b = new LobbyGlobePlace( "Second", new N.Vector3( 115, 0, 0 ), 0, 0, 5 );
		Assert.AreEqual( "bullfrog", a.ModelName ); Assert.AreEqual( "hpin", b.ModelName );
		Assert.AreSame( a, LobbyGlobeState.Pick( new N.Vector3( 475, 0, 0 ), -N.Vector3.UnitX, new[] { a, b }, LobbyGlobeMode.Visit ) );
		Assert.AreSame( b, LobbyGlobeState.Pick( new N.Vector3( 475, 0, 0 ), -N.Vector3.UnitX, new[] { a, b }, LobbyGlobeMode.Select ) );
		Assert.IsNull( LobbyGlobeState.Pick( new N.Vector3( 475, 0, 0 ), N.Vector3.UnitX, new[] { a }, LobbyGlobeMode.Visit ) );
		Assert.IsNull( LobbyGlobeState.Pick( new N.Vector3( 475, 0, 0 ), N.Vector3.UnitY, new[] { a }, LobbyGlobeMode.Visit ) );
		Assert.IsFalse( (b with { Count = 5 }).VisibleIn( LobbyGlobeMode.Select ) );
		Assert.AreEqual( 0.1f, (a with { Count = 1000 }).Size );
		Assert.AreEqual( 0.02f, (a with { Count = -1 }).Size );
	}
	[TestMethod]
	public void GlobePageModelsTexturesAndControlStatesResolveFromRetailData()
	{
		GameData.Require();
		foreach ( string name in new[] { "globe", "hpin", "bullfrog" } )
		{
			var model = new ModelFile( AnimationFiles.ResolveModel( $"lobby/globe/{name}.md2" ) );
			foreach ( var material in model.Meshes.SelectMany( mesh => mesh.Materials ) )
				Assert.IsTrue( new TextureFile( AnimationFiles.ResolveFile( $"lobby/globe/textures/{material.Name}.wct", "Globe texture" ) ).Data.Width > 0 );
		}
		foreach ( uint hash in new uint[] { 0x0d83e731, 0x1d1bc312, 0x76b2cc3c, 0x795da8e1, 0x390d8a1b, 0xf1d0542a, 0xee357a41, 0x19de04e0 } )
		{
			string path = LobbyControlResources.ResolveModel( hash );
			byte[] bytes = FileSystem.ReadAllBytes( path );
			for ( int state = 0; state < (hash == 0x0d83e731 ? 1 : 6); state++ )
			{
				var geometry = new LobbyButtonGeometry( bytes, state );
				foreach ( var material in geometry.VisibleMeshes.SelectMany( index => geometry.Model.Meshes[index].Materials ) )
					Assert.IsTrue( new TextureFile( AnimationFiles.ResolveFile( $"ui/textures/{material.Name}.wct", "Globe control texture" ) ).Data.Width > 0 );
			}
		}
	}
	[TestMethod]
	public void GlobeMaterialsBeyondSixteenAreBatchedWithoutDroppingTriangles()
	{
		GameData.Require();
		var globe = new ModelFile( AnimationFiles.ResolveModel( "lobby/globe/globe.md2" ) );
		Assert.IsTrue( globe.Meshes.Any( mesh => mesh.Materials.Length > 16 ) );
		foreach ( var mesh in globe.Meshes )
		{
			var batches = LobbyModelBatches.Create( mesh );
			Assert.AreEqual( mesh.Indices.Length, batches.Sum( batch => batch.Indices.Length ) );
			CollectionAssert.AreEquivalent( mesh.Indices, batches.SelectMany( batch => batch.Indices ).ToArray() );
			foreach ( var batch in batches )
			foreach ( uint index in batch.Indices )
				Assert.IsTrue( mesh.Vertices[index].TextureIndex - batch.FirstMaterial is >= 0 and < 16 );
		}
		var sea = new ModelFile( AnimationFiles.ResolveModel( "lobby/terrain/Base.md2" ) );
		foreach ( var mesh in sea.Meshes )
		{
			Assert.AreEqual( mesh.Indices.Length, LobbyModelBatches.Create( mesh ).Sum( batch => batch.Indices.Length ) );
			foreach ( var material in mesh.Materials ) Assert.IsTrue( new TextureFile( $"lobby/terrain/textures/{material.Name}.wct" ).Data.Width > 0 );
		}
	}
	[TestMethod]
	public void WorldMaterialBatchesKeepNativeAddressModesWithoutLosingFaces()
	{
		GameData.Require();
		bool clamped = false, wrapped = false;
		var paths = LobbyConfig.Load().Islands.Select( island => island.ModelPath )
			.Concat( new[] { "lobby/terrain/Base.md2", "lobby/globe/globe.md2" } );
		foreach ( string name in paths )
		{
			var model = new ModelFile( AnimationFiles.ResolveModel( name ) );
			foreach ( var mesh in model.Meshes )
			{
				var batches = LobbyModelBatches.Create( mesh );
				CollectionAssert.AreEquivalent( mesh.Indices, batches.SelectMany( batch => batch.Indices ).ToArray(), name );
				foreach ( var batch in batches )
				{
					clamped |= !batch.Wrap; wrapped |= batch.Wrap;
					foreach ( uint index in batch.Indices )
						Assert.AreEqual( (mesh.Materials[mesh.Vertices[index].TextureIndex].Flags & 0x30) != 0, batch.Wrap, name );
				}
			}
		}
		Assert.IsTrue( clamped && wrapped, "The shipped models must exercise both native address modes" );
	}
}
