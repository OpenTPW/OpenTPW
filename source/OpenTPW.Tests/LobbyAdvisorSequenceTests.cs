using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyAdvisorSequenceTests
{
	[TestMethod]
	public void NativeIntroductionConsumesRandomAndOutroDoesNot()
	{
		var durations = Enumerable.Repeat( 1000, 15 ).ToArray();
		durations[14] = 666;
		int calls = 0;
		int[] draws = { 7, 9, 0 };
		var sequence = LobbyAdvisorSequence.Create( 2500, durations, () => draws[calls++] );
		CollectionAssert.AreEqual( new[] { 13, 9, 0, 14 }, sequence.Segments );
		Assert.AreEqual( 3, calls );
		Assert.AreEqual( 3666, sequence.DurationMilliseconds );
	}

	[TestMethod]
	public void ShortRemainderSelectsFirstSufficientGestureAndLongSpeechIsBounded()
	{
		var durations = Enumerable.Repeat( 1000, 15 ).ToArray();
		var shortSpeech = LobbyAdvisorSequence.Create( 1000, durations, () => 9 );
		CollectionAssert.AreEqual( new[] { 13, 0, 14 }, shortSpeech.Segments );
		var longSpeech = LobbyAdvisorSequence.Create( 100000, durations, () => 9 );
		Assert.AreEqual( 20, longSpeech.Segments.Length );
		Assert.AreEqual( 13, longSpeech.Segments[0] );
		Assert.AreEqual( 14, longSpeech.Segments[^1] );
		Assert.ThrowsException<ArgumentException>( () => LobbyAdvisorSequence.Create( 0, new int[15], () => 0 ) );
	}
}
