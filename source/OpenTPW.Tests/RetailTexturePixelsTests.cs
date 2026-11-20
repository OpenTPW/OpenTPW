using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class RetailTexturePixelsTests
{
	[TestMethod]
	public void Argb4444KeepsTheTopNibbleOfEveryChannel()
	{
		byte[] pixel = [75, 211, 255, 76];
		RetailTexturePixels.QuantizeArgb4444( pixel );
		CollectionAssert.AreEqual( new byte[] { 68, 221, 255, 68 }, pixel );
		byte[] edges = [0, 15, 16, 255];
		RetailTexturePixels.QuantizeArgb4444( edges );
		CollectionAssert.AreEqual( new byte[] { 0, 0, 17, 255 }, edges );
	}

	[TestMethod]
	public void HorizonBandMatchesTheBackgroundAfterQuantizing()
	{
		GameData.Require();
		var image = RetailTexturePixels.DecodeTga( FileSystem.ReadAllBytes( LobbySkyGeometry.Root + "sky_cyl.tga" ) );
		var data = image.Data.ToArray();
		RetailTexturePixels.QuantizeArgb4444( data );
		// The opaque band of the horizon cylinder: at 8 bits (75,211,255) against 0x44ddff, in ARGB4444 identical.
		int opaque = Enumerable.Range( 0, data.Length / 4 ).Count( i => data[i * 4 + 3] == 255 );
		int background = Enumerable.Range( 0, data.Length / 4 ).Count( i => data[i * 4 + 3] == 255 && data[i * 4] == 68 && data[i * 4 + 1] == 221 && data[i * 4 + 2] == 255 );
		Assert.IsTrue( background > opaque / 2, $"{background} of {opaque} opaque horizon texels equal the background colour" );
	}
}
