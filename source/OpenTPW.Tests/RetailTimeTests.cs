using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace OpenTPW.Tests;

[TestClass]
public class RetailTimeTests
{
	[TestMethod]
	public void FrameScaleMatchesTheRetailInstructionAndConstant()
	{
		Assert.AreEqual( 0.16f, RetailTime.TicksFromSeconds( 0.016f ), 1e-6f );
		Assert.AreEqual( 1f, RetailTime.TicksFromSeconds( 0.1f ), 1e-6f );
		Assert.AreEqual( 4.99f, RetailTime.TicksFromSeconds( 0.499f ), 1e-5f );
		Assert.AreEqual( 5f, RetailTime.TicksFromSeconds( 0.5f ) );
		Assert.AreEqual( 5f, RetailTime.TicksFromSeconds( 1f ) );
		Assert.AreEqual( 5f, RetailTime.TicksFromSeconds( float.MaxValue ) );
		Assert.AreEqual( 0f, RetailTime.TicksFromSeconds( 0f ) );
		Assert.AreEqual( 0f, RetailTime.TicksFromSeconds( -1f ) );
	}

	[TestMethod]
	public void OneSecondOfNormalFramesAdvancesTenRetailUnits()
	{
		float elapsed = 0;
		for ( int frame = 0; frame < 60; frame++ ) elapsed += RetailTime.TicksFromSeconds( 1f / 60 );
		Assert.AreEqual( 10f, elapsed, 1e-4f );
		Assert.AreEqual( 1f, RetailTime.SecondsFromTicks( elapsed ), 1e-5f );
		// FUN_005d4c20: translation length = speed * dt for a normalized direction.
		Assert.AreEqual( 15f, 1.5f * elapsed, 1e-3f );
		Assert.AreEqual( 25f, 2.5f * elapsed, 1e-3f );
		Assert.AreEqual( 31, RetailTime.ParkStepMilliseconds );
	}

	[TestMethod]
	public void SecondsAndRetailUnitsRoundTripWithinTheFrameCap()
	{
		foreach ( var seconds in new[] { 0f, 0.001f, 0.016f, 0.25f, 0.5f } )
			Assert.AreEqual( seconds, RetailTime.SecondsFromTicks( RetailTime.TicksFromSeconds( seconds ) ), 1e-6f );
		Assert.AreEqual( 0.5f, RetailTime.SecondsFromTicks( RetailTime.TicksFromSeconds( 60 ) ) );
	}

	[TestMethod]
	public void NonFiniteTimesAreRejected()
	{
		foreach ( var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity } )
		{
			Assert.ThrowsException<ArgumentOutOfRangeException>( () => RetailTime.TicksFromSeconds( value ) );
			Assert.ThrowsException<ArgumentOutOfRangeException>( () => RetailTime.SecondsFromTicks( value ) );
		}
	}
}
