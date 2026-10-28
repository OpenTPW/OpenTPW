using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyUiTextLayoutTests
{
	// One unit per character, so widths are character counts.
	private static string[] Layout( string text, float width ) => LobbyUiText.Layout( text, width, part => part.Length );

	[TestMethod]
	public void LinesBreakAtTheLastBlankAndDropBlanksAroundTheBreak()
	{
		CollectionAssert.AreEqual( new[] { "aaa bbb", "ccc" }, Layout( "aaa bbb ccc", 7 ) );
		CollectionAssert.AreEqual( new[] { "aa", "bb" }, Layout( "aa   bb", 4 ) );
		CollectionAssert.AreEqual( new[] { "aa", "bb" }, Layout( "aa\tbb", 3 ) );
	}

	[TestMethod]
	public void CommasDoNotBreakAndLongWordsAreCut()
	{
		CollectionAssert.AreEqual( new[] { "aaa,b", "bb" }, Layout( "aaa,bbb", 5 ) );
		CollectionAssert.AreEqual( new[] { "a", "b" }, Layout( "ab", 0.5f ) );
	}

	[TestMethod]
	public void ParagraphsFollowTheRetailLineFeedAndCarriageReturnRules()
	{
		CollectionAssert.AreEqual( new[] { "A", "", "B" }, Layout( "A\n\nB", float.PositiveInfinity ) );
		CollectionAssert.AreEqual( new[] { "A" }, Layout( "A\n", float.PositiveInfinity ) );
		// A carriage return directly after a break joins it; one before a line feed does not.
		CollectionAssert.AreEqual( new[] { "A", "B" }, Layout( "A\n\rB", float.PositiveInfinity ) );
		CollectionAssert.AreEqual( new[] { "A", "", "B" }, Layout( "A\r\nB", float.PositiveInfinity ) );
		Assert.AreEqual( 0, Layout( "", 10 ).Length );
	}
}
