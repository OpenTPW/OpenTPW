using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class TextureOrientationTests
{
	[TestMethod]
	public void NativeTgaRowsAreBottomFirstForEitherFileOrigin()
	{
		byte[][] pixels = { new byte[] { 255, 0, 0, 255 }, new byte[] { 0, 255, 0, 255 },
			new byte[] { 0, 0, 255, 255 }, new byte[] { 255, 255, 0, 255 } };
		byte[] Expected() => new[] { pixels[2], pixels[3], pixels[0], pixels[1] }.SelectMany( x => x ).ToArray();
		foreach ( bool topOrigin in new[] { false, true } )
		{
			var tga = new byte[18 + 16];
			tga[2] = 2; tga[12] = 2; tga[14] = 2; tga[16] = 32; tga[17] = (byte)(8 | (topOrigin ? 32 : 0));
			var rows = topOrigin ? pixels : new[] { pixels[2], pixels[3], pixels[0], pixels[1] };
			for ( int i = 0; i < 4; i++ )
			{
				var rgba = rows[i];
				Array.Copy( new[] { rgba[2], rgba[1], rgba[0], rgba[3] }, 0, tga, 18 + i * 4, 4 );
			}
			CollectionAssert.AreEqual( Expected(), RetailTexturePixels.DecodeTga( tga ).Data );
		}
	}

	[TestMethod]
	public void RetailRaindropHasItsWideHeadAtLowVAndNarrowTailAtHighV()
	{
		GameData.Require();
		var image = LobbyWeather.ReadTexture( "raindrop.tga" );
		int WidthAt( int y ) => Enumerable.Range( 0, image.Width ).Count( x =>
			image.Data[(y * image.Width + x) * 4] > 64 );
		Assert.IsTrue( WidthAt( image.Height / 4 ) > WidthAt( image.Height * 3 / 4 ),
			"The falling end uses low V; flipping the decoded rows swaps the wide head and narrow tail." );
	}
}
