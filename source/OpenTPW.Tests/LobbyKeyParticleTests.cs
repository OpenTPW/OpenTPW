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
		Assert.ThrowsExactly<ArgumentOutOfRangeException>( () => Create().Start( 1, 0, 0 ) );
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
	public void ButtonEmitterKeepsItsAuthoredTranslucentColourAndStopsOnLeave()
	{
		var state = Create();
		state.Start( 35, 10000, 20000 );
		state.MoveButtonEmitter( 0, 50000, 30000 );
		for ( int i = 0; i < 80; i++ ) state.Tick();
		Assert.IsTrue( state.Particles.Any(), "The paused emitter lifetime keeps producing sprites while hovered" );
		Assert.IsTrue( state.Particles.All( p => p.Sequence == 12 && (p.Colour >> 24) <= 0x74 ) );
		state.Stop( 35 );
		for ( int i = 0; i < 25; i++ ) state.Tick();
		Assert.IsFalse( state.Particles.Any(), "Leaving the button drains existing sprites" );
	}
	[TestMethod]
	public void UiRepellerTargetsItsOwnParticleCategory()
	{
		GameData.Require();
		var library = FileSystem.ReadAllBytes( "Particle/Tp2.plb" );
		var disabled = (byte[])library.Clone();
		// Redirect the UI repeller to unused category 2; keep every other authored value identical.
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian( disabled.AsSpan( 33616 + 3 * 104 + 4 ), 2 );
		var original = new LobbyKeyParticles( library, 1024 );
		var redirected = new LobbyKeyParticles( disabled, 1024 );
		foreach ( var state in new[] { original, redirected } )
		{
			state.Start( 98, 50000, 30000 );
			state.Start( 90, 50000, 30000 );
			for ( int i = 0; i < 6; i++ ) state.Tick();
		}
		var puff = original.Particles.Where( p => p.Sequence == 15 ).ToArray();
		var otherPuff = redirected.Particles.Where( p => p.Sequence == 15 ).ToArray();
		Assert.IsTrue( puff.Length > 0 );
		CollectionAssert.AreEqual( puff.Select( p => (p.X, p.Z, p.Vx, p.Vz) ).ToArray(),
			otherPuff.Select( p => (p.X, p.Z, p.Vx, p.Vz) ).ToArray() );
		Assert.IsFalse( original.Particles.Where( p => p.Sequence == 12 ).Select( p => (p.X, p.Z, p.Vx, p.Vz) )
			.SequenceEqual( redirected.Particles.Where( p => p.Sequence == 12 ).Select( p => (p.X, p.Z, p.Vx, p.Vz) ) ) );
	}
	[TestMethod]
	public void FixedParticleClockIsIndependentOfRenderFrameSplits()
	{
		var a = Create(); var b = Create(); a.Start( 87, 90000, 7000 ); b.Start( 87, 90000, 7000 );
		a.Advance( 0.5f );
		for ( int i = 0; i < 4; i++ ) b.Advance( 0.125f );
		CollectionAssert.AreEqual( a.Particles.Select( p => (p.X, p.Y, p.Z, p.Life, p.Colour, p.Angle) ).ToArray(),
			b.Particles.Select( p => (p.X, p.Y, p.Z, p.Life, p.Colour, p.Angle) ).ToArray() );
		Assert.ThrowsExactly<ArgumentOutOfRangeException>( () => a.Advance( float.NaN ) );
	}
	[TestMethod]
	public void ScreenParticlesUseTheRetailDivisors()
	{
		// Particle_DrawScreenEffect (US address pending) uses 49 and 37; the reviewed full range is about ±0.997 and ±0.990.
		Assert.AreEqual( 0f, LobbyKeyParticlePanel.ScreenPosition( 3125, 0 ).X );
		Assert.AreEqual( 50000 / 49 / 1024f, LobbyKeyParticlePanel.ScreenPosition( 6250, 0 ).X );
		Assert.AreEqual( -(37500 / 37) / 1024f, LobbyKeyParticlePanel.ScreenPosition( 0, 0 ).Y );
		// The naming dialog's OK button centre (1418, 566) lands within a pixel of its UI rectangle at 1024 × 768.
		var (x, y) = LobbyKeyParticlePanel.ScreenPosition( (int)((1418 / 1024.0) * 50000) >> 4, (int)((566 / 768.0) * 37500) >> 4 );
		Assert.AreEqual( (1418 - 1024) / 1024.0, x, 1.5 / 512 );
		Assert.AreEqual( (566 - 768) / 768.0, y, 1.5 / 384 );
	}
}
