using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyFadeTests
{
	[TestMethod]
	public void FadeKeepsTheNativeFixedPointRoundingAndRecoversAfterCancellation()
	{
		var fade = new LobbyFade();
		fade.Update( 1 ); Assert.AreEqual( 8192, fade.Value ); Assert.AreEqual( (byte)32, fade.Alpha );
		fade.Update( 1 ); Assert.AreEqual( 15360, fade.Value );
		fade.Update( 0 ); Assert.AreEqual( 13440, fade.Value );
		for ( int i = 0; i < 100; i++ ) fade.Update( 0 );
		Assert.AreEqual( 0, fade.Value );
		for ( int i = 0; i < 100; i++ ) fade.Update( 1 );
		Assert.AreEqual( (byte)255, fade.Alpha );
	}
}
