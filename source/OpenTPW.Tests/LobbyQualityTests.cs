using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyQualityTests
{
	[TestMethod]
	public void RetailPresetsDriveCountsTexturesAndWeather()
	{
		GameData.Require();
		var meshes = LobbyConfig.Load().Islands.SelectMany( x => x.FlyingMeshes ).ToArray();
		var presets = new[] { "low", "med", "high" };
		var percentages = new[] { 10, 70, 100 };
		var counts = new[] { new[] { 1, 1, 5 }, new[] { 7, 7, 35 }, new[] { 10, 10, 50 } };
		for ( int i = 0; i < presets.Length; i++ )
		{
			var quality = LobbyQuality.Load( presets[i] );
			Assert.AreEqual( percentages[i], quality.ObjectsPercent );
			Assert.AreEqual( i + 1, quality.TextureQuality );
			Assert.AreEqual( i == 0, quality.UseSmallTextures );
			Assert.AreEqual( i != 0, quality.WeatherEnabled );
			CollectionAssert.AreEqual( counts[i], meshes.Select( x => x.CountAtQuality( quality.ObjectsPercent ) ).ToArray() );
		}
	}

	[TestMethod]
	public void EveryPresetHasReadableLobbyTextures()
	{
		GameData.Require();
		foreach ( var preset in new[] { "low", "med", "high" } )
		{
			var quality = LobbyQuality.Load( preset );
			int checkedTextures = 0;
			foreach ( var island in LobbyConfig.Load().Islands )
			{
				var models = new[] { (island.ModelPath, island.ResourceDirectory), (island.GateModelPath, island.ResourceDirectory) }
					.Concat( island.FlyingMeshes.Select( x => (x.ModelPath, x.ResourceDirectory) ) );
				foreach ( var (modelPath, directory) in models )
				{
					foreach ( var name in new ModelFile( modelPath ).Meshes.SelectMany( mesh => mesh.Materials )
						.Select( material => material.Name?.TrimEnd( '\0' ) ).Where( name => !string.IsNullOrEmpty( name ) ).Distinct() )
					{
						// SGN is generated. signgrab has no shipped WCT and uses the native cache fallback.
						if ( name!.Equals( "sign1", StringComparison.OrdinalIgnoreCase ) || name.Equals( "sign2", StringComparison.OrdinalIgnoreCase ) || name.Equals( "signgrab", StringComparison.OrdinalIgnoreCase ) ) continue;
						var path = AnimationFiles.ResolveFile( $"{quality.TextureDirectory( directory + "/textures" )}/{name}.wct", "Texture" );
						var texture = new TextureFile( path ).Data;
						Assert.IsTrue( texture.Width > 0 && texture.Height > 0, path );
						checkedTextures++;
					}
				}
			}
			Assert.IsTrue( checkedTextures > 0, preset );
		}
	}

	[TestMethod]
	public void LowQualityOnlyReplacesTheTextureDirectoryComponent()
	{
		GameData.Require();
		var quality = LobbyQuality.Load( "LOW" );
		Assert.AreEqual( "lobby/terrain/stexture", quality.TextureDirectory( "lobby\\terrain\\textures\\" ) );
		Assert.AreEqual( "lobby/textures/model", quality.TextureDirectory( "lobby/textures/model" ) );
		Assert.AreEqual( "stexture", quality.TextureDirectory( "textures" ) );
		Assert.ThrowsException<ArgumentException>( () => LobbyQuality.Load( "../custom" ) );
	}
}
