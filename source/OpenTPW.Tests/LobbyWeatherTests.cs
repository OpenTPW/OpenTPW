using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyWeatherTests
{
	[TestMethod]
	public void RainUsesFourthPowerOfMeanSquaredHorizontalDistance()
	{
		// TP.exe 0x5dbe7f..0x5dbed1: float arithmetic, threshold, cap, then truncation.
		Assert.AreEqual( 160, LobbyWeather.CalculateRainAmount( 5000, 1 ) );
		Assert.AreEqual( 400, LobbyWeather.CalculateRainAmount( 1000, 1 ) );
		Assert.AreEqual( 0, LobbyWeather.CalculateRainAmount( 10000, 1 ) );
		Assert.AreEqual( 0, LobbyWeather.CalculateRainAmount( 40000, 1 ) );
	}

	[TestMethod]
	public void RainCapsAtItsCenterAndRequiresARainyIsland()
	{
		Assert.AreEqual( 400, LobbyWeather.CalculateRainAmount( 0, 1 ) );
		Assert.AreEqual( 0, LobbyWeather.CalculateRainAmount( 0, 0 ) );
	}

	[TestMethod]
	public void WeatherUsesSourceAlphaPlusDestinationAndOtherMaterialsKeepTheirBlend()
	{
		var weather = Material.BlendState( MaterialFlags.Additive ).AttachmentStates[0];
		Assert.AreEqual( Veldrid.BlendFactor.SourceAlpha, weather.SourceColorFactor );
		Assert.AreEqual( Veldrid.BlendFactor.One, weather.DestinationColorFactor );
		var flash = Material.BlendState( MaterialFlags.InvertDestination ).AttachmentStates[0];
		Assert.AreEqual( Veldrid.BlendFactor.InverseDestinationColor, flash.SourceColorFactor );
		Assert.AreEqual( Veldrid.BlendFactor.Zero, flash.DestinationColorFactor );
		Assert.AreEqual( Veldrid.BlendFactor.InverseSourceAlpha,
			Material.BlendState( MaterialFlags.None ).AttachmentStates[0].DestinationColorFactor );
	}

	[TestMethod]
	public void AmbientRequestsConsumeTheSharedLobbyStreamWithoutLightning()
	{
		var random = new LobbyRandom( 12345 );
		var simulation = new LobbyWeatherSimulation( random, () => 2 );
		simulation.Update( 0, Vector3.Zero, 0, true );
		Assert.AreEqual( 0xe22029c8u, random.State );
		Assert.IsFalse( simulation.Flash );
		Assert.AreEqual( 0, simulation.Bolts.Count );
	}

	[TestMethod]
	public void BothRetailWeatherTexturesLoadFromTheSelectedGameData()
	{
		GameData.Require();
		foreach ( var name in new[] { "raindrop.tga", "lightning.tga" } )
		{
			var image = LobbyWeather.ReadTexture( name );
			Assert.IsTrue( image.Width > 0 && image.Height > 0 );
			Assert.AreEqual( image.Width * image.Height * 4, image.Data.Length );
		}
	}

	[TestMethod]
	public void BoltsKeepRetailEndpointsQueueLimitAndHalfSecondLifetime()
	{
		var simulation = new LobbyWeatherSimulation( new LobbyRandom( 12345 ), () => 2 );
		var center = new Vector3( 400, 600, 35 );
		for ( int i = 0; i < 200; i++ ) simulation.Update( 1, center, 0, true );
		Assert.AreEqual( 16, simulation.Bolts.Count );
		foreach ( var bolt in simulation.Bolts )
		{
			Assert.AreEqual( 500f, bolt.Top.Z );
			Assert.AreEqual( 0f, bolt.Bottom.Z );
			Assert.AreEqual( 3u, bolt.Seed );
			Assert.IsTrue( MathF.Abs( bolt.Top.X - center.X ) <= 50 );
			Assert.IsTrue( MathF.Abs( bolt.Top.Y - center.Y ) <= 50 );
			Assert.IsTrue( MathF.Abs( bolt.Bottom.X - bolt.Top.X ) <= 10 );
			Assert.IsTrue( MathF.Abs( bolt.Bottom.Y - bolt.Top.Y ) <= 10 );
		}
		simulation.Update( 0, center, 0.499f, true );
		Assert.AreEqual( 16, simulation.Bolts.Count );
		Assert.IsFalse( simulation.Flash );
		simulation.Update( 0, center, 0.002f, true );
		Assert.AreEqual( 0, simulation.Bolts.Count );
	}

	[TestMethod]
	public void CrtRainAndIdleRandomSequenceMatchesRetail()
	{
		uint seed = 1;
		CollectionAssert.AreEqual( new[] { 41, 18467, 6334, 26500, 19169 },
			Enumerable.Range( 0, 5 ).Select( _ => RetailRandom.Advance( ref seed ) ).ToArray() );
	}

	[TestMethod]
	public void RainCubeConsumesNativeAxisOrderAndScrollsFiveUnitsPerSecond()
	{
		int draws = 0;
		var rain = new LobbyRainSimulation( () => new[] { 8192, 10240, 12288 }[(draws++) % 3] );
		Assert.AreEqual( 3072, draws );
		Assert.AreEqual( new Vector3( 0, 2, 1 ), rain.Point( 0, Vector3.Zero ) );
		rain.Advance( 0.2f );
		// 0x579f30's last stores, not its first normalization: layer-3 residual wind.
		var moved = rain.Point( 0, Vector3.Zero );
		Assert.AreEqual( -0.00010195f, moved.X, 0.00000001f );
		Assert.AreEqual( 2.0000052f, moved.Y, 0.0000003f );
		Assert.AreEqual( 0f, moved.Z );
		Assert.AreEqual( -1f, LobbyRainSimulation.Direction.Z );
		Assert.AreEqual( -4f, LobbyRainSimulation.CameraOffset( 40 ) );
		Assert.AreEqual( -3f, LobbyRainSimulation.Wrap( 5 ) );
		Assert.AreEqual( 3f, LobbyRainSimulation.Wrap( -5 ) );
		Assert.AreEqual( 4f, LobbyRainSimulation.Wrap( 4 ) );
	}

	[TestMethod]
	public void SeededLightningSubdivisionKeepsEndpointsAndBranchDepth()
	{
		var top = new Vector3( 400, 600, 500 );
		var bottom = new Vector3( 405, 598, 0 );
		var geometry = new LobbyLightningGeometry();
		var strokes = geometry.Generate( top, bottom, 3 );
		var main = strokes.Single( x => x.Depth == 0 );
		Assert.AreEqual( bottom, main.Points.First() );
		Assert.AreEqual( top, main.Points.Last() );
		Assert.IsTrue( main.Points.Count > 25 );
		Assert.IsTrue( strokes.All( x => x.Depth >= 0 && x.Depth <= 4 ) );
		Assert.IsTrue( strokes.SelectMany( x => x.Points ).All( p => float.IsFinite( p.X ) && float.IsFinite( p.Y ) && float.IsFinite( p.Z ) ) );
		var points = main.Points.ToArray();
		CollectionAssert.AreEqual( points, geometry.Generate( top, bottom, 3 ).Single( x => x.Depth == 0 ).Points.ToArray() );
	}

	[TestMethod]
	public void RainMapsItsUpperTailToVmaxAndFallingHeadToVmin()
	{
		var vertices = new Vertex[6];
		int offset = 0;
		var tail = new System.Numerics.Vector4( 0, 1, 0.001f, 1 );
		var head = new System.Numerics.Vector4( 0, 0, 0.001f, 1 );
		LobbyWeather.RainQuad( vertices, ref offset, tail, head, 1 );
		Assert.AreEqual( 1f, vertices[0].TexCoords.Y );
		Assert.AreEqual( 0f, vertices[1].TexCoords.Y );
		Assert.AreEqual( 1f, vertices[4].TexCoords.Y );
		Assert.AreEqual( 1f, vertices[0].Position.Y );
		Assert.AreEqual( 0f, vertices[1].Position.Y );
		Assert.AreEqual( 128u, vertices[0].MatFlags );
		Assert.AreEqual( 255u, vertices[1].MatFlags );
	}

	[TestMethod]
	public void RainLocationMatchesRetailCameraSnapshotsAndIgnoresHeight()
	{
		// Owned retail runtime snapshots sample-23/24/27; the rainy anchor is retail (600,38,600).
		var centers = new[] { new Vector3( 600, 600, 38 ) };
		Assert.AreEqual( 22, LobbyWeather.CalculateRainAmount( new Vector3( 516.181f, 566.202f, 68 ), centers ) );
		Assert.AreEqual( 45, LobbyWeather.CalculateRainAmount( new Vector3( 523.512f, 568.242f, 68 ), centers ) );
		Assert.AreEqual( 400, LobbyWeather.CalculateRainAmount( new Vector3( 545.801f, 574.442f, 68 ), centers ) );
		Assert.AreEqual( 22, LobbyWeather.CalculateRainAmount( new Vector3( 516.181f, 566.202f, 10000 ), centers ) );
		Assert.AreEqual( 0, LobbyWeather.CalculateRainAmount( new Vector3( 400, 400, 0 ), centers ) );
	}
}
