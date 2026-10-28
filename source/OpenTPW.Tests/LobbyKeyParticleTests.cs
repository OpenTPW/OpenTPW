using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyKeyParticleTests
{
	private static LobbyKeyParticles Create( int density = 1024 )
	{
		GameData.Require();
		return new LobbyKeyParticles( FileSystem.ReadAllBytes( "Particle/Tp2.plb" ), density );
	}
	[TestMethod]
	public void KeyUsesNativeFirstTickRandomLifeColourSizeAndRotation()
	{
		var state = Create(); state.Start( 87, 90000, 7000 );
		Assert.IsFalse( state.Particles.Any() );
		state.Tick(); Assert.IsFalse( state.Particles.Any() );
		state.Tick();
		var p = state.Particles.Single();
		// Three emitter spread calls precede the lifetime draw: SAR 16 = -6268, %25 = -18.
		Assert.AreEqual( 82, p.InitialLife ); Assert.AreEqual( 81, p.Life );
		Assert.AreEqual( 5625, p.X ); Assert.AreEqual( 437, p.Z );
		Assert.AreEqual( (short)4037, p.Size ); Assert.AreEqual( (ushort)600, p.Angle );
		Assert.AreEqual( 0xffffc100u, p.Colour );
		state.Tick(); Assert.AreEqual( 1, state.Particles.Count() );
		state.Tick(); Assert.AreEqual( 2, state.Particles.Count() );
		for ( int i = 0; i < 145; i++ ) state.Tick();
		Assert.IsTrue( state.Particles.Any( particle => particle.Sequence == 12 ), "The attractor must start Key2" );
		for ( int i = 0; i < 300; i++ ) state.Tick();
		Assert.IsFalse( state.Particles.Any(), "Expired emitters must drain their particles" );
	}
	[TestMethod]
	public void PuffUsesAuthoredInitialBurstAndDensityScaling()
	{
		foreach ( var (density, count) in new[] { (1024, 25), (1000, 24), (500, 12) } )
		{
			var state = Create( density ); state.Start( 98, 50000, 14794 );
			Assert.AreEqual( count, state.Particles.Count() );
			Assert.IsTrue( state.Particles.All( p => p.Life >= 20 && p.Life <= 30 ) );
			for ( int i = 0; i < 100; i++ ) state.Tick();
			Assert.IsFalse( state.Particles.Any() );
		}
		Assert.ThrowsException<ArgumentOutOfRangeException>( () => Create().Start( 1, 0, 0 ) );
	}
	[TestMethod]
	public void AffordableKeySparklePersistsAndDrainsAfterPageDeparture()
	{
		var state = Create(); state.Start( 97, 50000, 14794 );
		// The authored emitter has a frozen life field, but its individual sprites expire.
		for ( int i = 0; i < 300; i++ ) state.Tick();
		Assert.IsTrue( state.Particles.Any() );
		Assert.IsTrue( state.Particles.All( p => p.Sequence == 12 && p.Life <= 17 ) );
		state.Stop( 97 );
		Assert.IsTrue( state.Particles.Any(), "Stopping births must retain existing sprites" );
		for ( int i = 0; i < 25; i++ ) state.Tick();
		Assert.IsFalse( state.Particles.Any(), "Leaving an affordable island must drain the effect" );
	}
	[TestMethod]
	public void FixedParticleClockIsIndependentOfRenderFrameSplits()
	{
		var a = Create(); var b = Create(); a.Start( 87, 90000, 7000 ); b.Start( 87, 90000, 7000 );
		a.Advance( 0.5f );
		for ( int i = 0; i < 4; i++ ) b.Advance( 0.125f );
		CollectionAssert.AreEqual( a.Particles.Select( p => (p.X, p.Y, p.Z, p.Life, p.Colour, p.Angle) ).ToArray(),
			b.Particles.Select( p => (p.X, p.Y, p.Z, p.Life, p.Colour, p.Angle) ).ToArray() );
		Assert.ThrowsException<ArgumentOutOfRangeException>( () => a.Advance( float.NaN ) );
	}
}
