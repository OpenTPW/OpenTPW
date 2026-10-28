using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class TextureDecodeTests
{
	[TestMethod]
	public void BothWctTypesReconstructConstantLuminanceAtNativeSize()
	{
		// With unit quantization, 128 in each low-pass coefficient becomes 64 after
		// the two normalized D4 passes; all detail and chroma coefficients are zero.
		foreach ( byte type in new byte[] { 0x12, 0x13 } )
		{
			using var coefficients = new MemoryStream();
			using ( var writer = new BinaryWriter( coefficients, System.Text.Encoding.ASCII, true ) )
			{
				for ( int row = 0; row < 8; row++ )
				for ( int column = 0; column < 8; column++ )
				{
					if ( row < 4 && (type == 0x12 ? column < 4 : column % 2 == 0) ) { writer.Write( (byte)0x80 ); writer.Write( (short)128 ); }
					else writer.Write( (byte)0 );
				}
				writer.Write( new byte[32] );
			}
			byte[] chunk = Compress( coefficients.ToArray(), type );
			using var file = new MemoryStream();
			using ( var writer = new BinaryWriter( file, System.Text.Encoding.ASCII, true ) )
			{
				writer.Write( type ); writer.Write( (byte)0 ); writer.Write( (byte)24 ); writer.Write( (byte)4 );
				writer.Write( (short)8 ); writer.Write( (short)8 ); writer.Write( new byte[8] );
				writer.Write( chunk.Length ); writer.Write( 0 ); writer.Write( 0 );
				writer.Write( chunk );
			}
			file.Position = 0;
			var image = new TextureFile( file ).Data;
			Assert.AreEqual( 8 * 8 * 4, image.Data.Length );
			for ( int pixel = 0; pixel < 64; pixel++ )
			{
				for ( int channel = 0; channel < 3; channel++ ) Assert.AreEqual( 64, (int)image.Data[pixel * 4 + channel], 1, $"Type {type:X2}, pixel {pixel}, channel {channel}" );
				Assert.AreEqual( (byte)255, image.Data[pixel * 4 + 3] );
			}
		}
	}
	private static byte[] Compress( byte[] data, byte type )
	{
		if ( type == 0x13 )
		{
			int bits = data.Length * 9 + 13;
			var encoded = new byte[(bits + 15) / 16 * 2];
			for ( int index = 0; index < data.Length; index++ )
			for ( int bit = 0; bit < 8; bit++ )
				if ( (data[index] & (1 << bit)) != 0 ) encoded[(index * 9 + 1 + bit) / 8] |= (byte)(1 << ((index * 9 + 1 + bit) % 8));
			encoded[data.Length * 9 / 8] |= (byte)(1 << (data.Length * 9 % 8));
			return encoded;
		}
		using var compressed = new MemoryStream();
		using ( var zlib = new ZLibStream( compressed, CompressionMode.Compress, true ) ) zlib.Write( data );
		using var chunk = new MemoryStream();
		using var writer = new BinaryWriter( chunk );
		writer.Write( System.Text.Encoding.ASCII.GetBytes( "BILZ" ) );
		writer.Write( data.Length ); writer.Write( (int)compressed.Length );
		writer.Write( 15 ); writer.Write( 9 ); writer.Write( 0 ); writer.Write( 0 );
		writer.Write( compressed.ToArray() );
		return chunk.ToArray();
	}
	[TestMethod]
	public void RetailAdvisorWhiteHasNoFalseBoundaryStripes()
	{
		GameData.Require();
		var image = new TextureFile( "global/advisor/textures/White.wct" ).Data;
		var values = image.Data.Where( (_, index ) => index % 4 != 3 ).ToArray();
		Assert.IsTrue( values.Max() - values.Min() <= 1, $"White range: {values.Min()}..{values.Max()}" );
		// Shipped low-pass coefficients are 42, with Y quantization 11: 42 * 11 / 2 = 231.
		foreach ( byte value in values ) Assert.AreEqual( 231, (int)value, 1 );
	}
}
