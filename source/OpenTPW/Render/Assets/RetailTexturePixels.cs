using StbImageSharp;

namespace OpenTPW;

/// <summary>Pixel storage for native procedural UVs, as produced by 0x571210/0x5717a0.</summary>
internal static class RetailTexturePixels
{
	internal static ImageResult DecodeTga( byte[] bytes )
	{
		var image = ImageResult.FromMemory( bytes, ColorComponents.RedGreenBlueAlpha );
		// Stb returns visual top first; the retail TGA converter stores visual bottom first.
		// Keep this conversion out of shaders and geometry: their native V values stay unchanged.
		int stride = image.Width * 4;
		var row = new byte[stride];
		for ( int y = 0; y < image.Height / 2; y++ )
		{
			int top = y * stride, bottom = (image.Height - 1 - y) * stride;
			Buffer.BlockCopy( image.Data, top, row, 0, stride );
			Buffer.BlockCopy( image.Data, bottom, image.Data, top, stride );
			Buffer.BlockCopy( row, 0, image.Data, bottom, stride );
		}
		return image;
	}

	/// <summary>
	/// Retail loads every texture in one global format (TW 0x8c5c4c, US 0x8bd554: 0 RGB565, 1 ARGB1555,
	/// 2 ARGB4444, 3 32-bit), and the US D3D build runs with 2. 0x55f5a0 keeps the top four bits of each 8-bit
	/// channel; the device expands nibble n to n × 17. The art depends on it: sky_cyl.tga's (75,211,255)
	/// becomes the background colour 0x44ddff.
	/// </summary>
	internal static void QuantizeArgb4444( Span<byte> rgba )
	{
		for ( int i = 0; i < rgba.Length; i++ ) rgba[i] = (byte)((rgba[i] >> 4) * 17);
	}
}
