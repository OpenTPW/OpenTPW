using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using N = System.Numerics;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyFlockTests
{
	private static LobbyFlyingMeshConfig Config( int count = 50, float speed = 2.5f ) =>
		new( "lobby/hallow", "hbat", count, new N.Vector3( 200, 100, 200 ), speed );
	private static LobbyFlockSimulation Simulation( uint seed, int count = 50, float speed = 2.5f ) =>
		new( Config( count, speed ), new Vector3(), new Vector3( 200, 200, 100 ), new LobbyRandom( seed ) );

	[TestMethod]
	public void RetailSeedReplenishmentAndAxisOrderMatchTheRecordedSequence()
	{
		var random = new LobbyRandom( 0x12345 );
		var simulation = new LobbyFlockSimulation( Config(), new Vector3(), new Vector3( 200, 200, 100 ), random );
		var counts = new[] { 8, 11, 32, 37, 39 };
		var states = new uint[] { 0x5d3ac1c1, 0xb25bc8b9, 0x769b95c1, 0xf82dde39, 0xe9a111f9 };
		for ( int i = 0; i < counts.Length; i++ )
		{
			simulation.Update( 0, 100 );
			Assert.AreEqual( counts[i], simulation.Actors.Count, $"Frame {i}" );
			Assert.AreEqual( states[i], random.State, $"Frame {i}" );
		}
		Near( new Vector3( -3.111923f, -33.82394f, 33.436035f ), simulation.Actors[0].Position );
		Near( new Vector3( -48.57138f, -85.07542f, -37.79247f ), simulation.Actors[0].Target );
	}

	[TestMethod]
	public void PresetCountsAndSharedRandomStreamAreUsedByTheSimulation()
	{
		GameData.Require();
		foreach ( var preset in new[] { "low", "med", "high" } )
		{
			var quality = LobbyQuality.Load( preset );
			var random = new LobbyRandom( 123 );
			var groups = LobbyConfig.Load().Islands.SelectMany( x => x.FlyingMeshes )
				.Select( mesh => new LobbyFlockSimulation( mesh, new Vector3(), new Vector3( 200, 200, 100 ), random ) ).ToArray();
			for ( int frame = 0; frame < 100; frame++ )
				foreach ( var group in groups ) group.Update( 0, quality.ObjectsPercent );
			var expected = preset == "low" ? new[] { 1, 1, 5 } : preset == "med" ? new[] { 7, 7, 35 } : new[] { 10, 10, 50 };
			CollectionAssert.AreEqual( expected, groups.Select( x => x.Actors.Count ).ToArray() );
			Assert.AreNotEqual( groups[0].Actors[0].Position, groups[1].Actors[0].Position );
		}
	}

	[TestMethod]
	public void TargetThresholdIsStrictAndDoesNotConsumeAnExtraSpawnDraw()
	{
		var random = new LobbyRandom( 1 );
		var simulation = new LobbyFlockSimulation( Config( 1 ), new Vector3(), new Vector3( 200, 200, 100 ), random );
		simulation.Update( 0, 100 );
		uint state = random.State;
		var actor = simulation.Actors[0];
		actor.Position = new Vector3();
		actor.Target = new Vector3( 10, 20, 0 ); // Exactly 500 squared units.
		simulation.Update( 0, 100 );
		Assert.AreEqual( state, random.State );
		actor.Target = new Vector3( 10, 19, 0 );
		simulation.Update( 0, 100 );
		Assert.AreNotEqual( new Vector3( 10, 19, 0 ), actor.Target );
		var expected = new LobbyRandom( state );
		expected.Point( new Vector3(), new Vector3( 200, 200, 100 ) );
		Assert.AreEqual( expected.State, random.State );
	}

	[TestMethod]
	public void SteeringAndTravelUseRetailTicks()
	{
		var simulation = Simulation( 1, 1, 1.5f );
		simulation.Update( 0, 100 );
		var actor = simulation.Actors[0];
		actor.Position = new Vector3();
		actor.Direction = new Vector3( 1, 0, 0 );
		actor.Target = new Vector3( 0, 100, 0 );
		simulation.Update( 1, 100 );
		Near( new Vector3( 0.9f, 0.1f, 0 ).Normal, actor.Direction );
		Assert.AreEqual( 1.5f, actor.Position.Length, 0.00001f );
		// A straight actor travels 15 units in one second, not 150.
		actor.Position = new Vector3();
		actor.Direction = new Vector3( 0, 1, 0 );
		for ( int frame = 0; frame < 10; frame++ ) simulation.Update( 1, 100 );
		Near( new Vector3( 0, 15, 0 ), actor.Position );
	}

	[TestMethod]
	public void FacingMatchesTheRetailMatrixAfterSwappingYAndZ()
	{
		foreach ( var direction in new[] { new Vector3( 1, 0, 0 ), new Vector3( 0, 1, 0 ), new Vector3( 2, 3, 4 ), new Vector3( -2, -3, 4 ) } )
		{
			var forward = direction.Normal;
			float horizontal = MathF.Sqrt( forward.X * forward.X + forward.Y * forward.Y );
			var right = new Vector3( forward.Y / horizontal, -forward.X / horizontal, 0 );
			var up = new Vector3( -forward.X * forward.Z / horizontal, -forward.Y * forward.Z / horizontal, horizontal );
			var rotation = LobbyFlock.Facing( direction );
			Near( right, N.Vector3.Transform( N.Vector3.UnitX, rotation ) );
			Near( forward, N.Vector3.Transform( N.Vector3.UnitY, rotation ) );
			Near( up, N.Vector3.Transform( N.Vector3.UnitZ, rotation ) );
		}
	}

	private static void Near( Vector3 expected, Vector3 actual ) =>
		Assert.IsTrue( (expected - actual).Length < 0.00003f, $"Expected {expected}; actual {actual}" );
	private static void Near( Vector3 expected, N.Vector3 actual ) => Near( expected, new Vector3( actual.X, actual.Y, actual.Z ) );
}
