using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
namespace OpenTPW.Tests;
[TestClass]
public class LobbyCameraTests
{
	private static LobbyCameraState Create()
	{
		GameData.Require();
		return new LobbyCameraState( LobbyConfig.Load(), new LobbyRandom( 12345 ) );
	}
	[TestMethod]
	public void ConstructorConsumesFourRetailPointsInOrder()
	{
		GameData.Require();
		var random = new LobbyRandom( 12345 );
		var state = new LobbyCameraState( LobbyConfig.Load(), random );
		var expected = new LobbyRandom( 12345 );
		for ( int i = 0; i < 12; i++ ) expected.Next();
		Assert.AreEqual( expected.State, random.State );
		Assert.AreEqual( LobbyCameraState.Movement.Roaming, state.Mode );
	}
	[TestMethod]
	public void OrbitInterpolatesPreviousPoseUsingRetailTicks()
	{
		var state = Create();
		state.SelectIsland( 0 );
		state.Update( 1 );
		Assert.AreEqual( 0.02f, state.Angle, 0.000001f );
		Near( new Vector3( (400 + MathF.Sin( 0.02f ) * 70) * 0.1f, (400 + MathF.Cos( 0.02f ) * 70) * 0.1f, 3.25f ), state.Position );
		Near( new Vector3( 80, 80, 2.5f ), state.Target );
		var previous = state.Position;
		state.Update( 1 );
		Near( previous + (new Vector3( 400 + MathF.Sin( 0.04f ) * 70, 400 + MathF.Cos( 0.04f ) * 70, 32.5f ) - previous) * 0.1f, state.Position );
	}
	[TestMethod]
	public void OrbitWrapsAngleAndCapsFrameAtFiveTicks()
	{
		var state = Create(); state.SelectIsland( 0 );
		for ( int i = 0; i < 63; i++ ) state.Update( 500 );
		Assert.AreEqual( 6.3f - MathF.Tau, state.Angle, 0.00001f );
		Assert.AreEqual( 70f, state.Radius );
	}
	[TestMethod]
	public void NonFiniteTicksAreRejected()
	{
		var state = Create(); state.SelectIsland( 0 );
		Assert.ThrowsException<System.IO.InvalidDataException>( () => state.Update( float.NaN ) );
	}
	[TestMethod]
	public void SwitchingIslandsPreservesPoseAndAngle()
	{
		var state = Create(); state.SelectIsland( 0 ); state.Update( 1 );
		var previous = state.Position;
		state.SelectIsland( 2 );
		Assert.AreEqual( previous, state.Position );
		Assert.AreEqual( 0.02f, state.Angle, 0.000001f );
		state.Update( 1 );
		Near( previous + (new Vector3( 600 + MathF.Sin( 0.04f ) * 70, 600 + MathF.Cos( 0.04f ) * 70, 58 ) - previous) * 0.1f, state.Position );
	}
	[TestMethod]
	public void EntranceAlignsBeforeOpeningAndClosesOnlyAfterOpening()
	{
		var state = Create(); state.SelectIsland( 3 ); state.Update( 1 ); state.BeginEntering();
		Assert.IsFalse( state.CancelEntering() ); state.BeginEntering();
		for ( int i = 0; i < 1000 && state.Mode == LobbyCameraState.Movement.Aligning; i++ ) state.Update( 0.8f );
		Assert.AreEqual( LobbyCameraState.Movement.Entering, state.Mode );
		Assert.IsTrue( state.GateOpened ); Assert.AreEqual( 70f, state.Radius );
		state.Update( 1 );
		Assert.AreEqual( MathF.PI, state.Angle ); Assert.AreEqual( 65.1f, state.Radius, 0.00001f );
		Assert.AreEqual( 8f, state.Height, 0.00001f );
		Assert.AreEqual( 1 - (65.1f - 8) / 62, state.EntryFade, 0.00001f );
		Assert.IsFalse( state.GateOpened ); Assert.IsTrue( state.CancelEntering() );
		Assert.AreEqual( LobbyCameraState.Movement.Orbiting, state.Mode ); Assert.AreEqual( 0f, state.EntryFade );
	}
	[TestMethod]
	public void RoamingStartsAtSeededPositionAndMovesOneUnitPerTick()
	{
		var state = Create(); var expected = new LobbyRandom( 12345 );
		var center = new Vector3( 500, 500, 75 ); var extents = new Vector3( 400, 400, 50 );
		var start = expected.Point( center, extents ); var destination = expected.Point( center, extents );
		state.Update( 1 ); Near( start + (destination - start).Normal, state.Position );
		Assert.AreEqual( 0, state.CurrentIslandId ); Assert.AreEqual( 1f, (state.Position - start).Length, 0.0001f );
	}
	private static void Near( Vector3 expected, Vector3 actual ) => Assert.IsTrue( (expected - actual).LengthSquared < 0.00001f, $"Expected {expected}, actual {actual}" );
}
