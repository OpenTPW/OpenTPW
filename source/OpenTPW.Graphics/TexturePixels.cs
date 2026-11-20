namespace OpenTPW.Graphics;

/// <summary>Shared RGBA8 upload and mip-chain creation; colour-format conversion remains the caller's policy.</summary>
public static class TexturePixels
{
	public static GpuTexture Create( GpuDevice device, ReadOnlySpan<byte> rgba, uint width, uint height, bool mipmaps = true )
	{
		uint levels = mipmaps ? (uint)System.Numerics.BitOperations.Log2( Math.Max( width, height ) ) + 1 : 1;
		var texture = GpuTexture.Create2D( device, width, height, levels: levels );
		try
		{
			texture.Upload( rgba );
			if ( levels > 1 ) texture.GenerateMipmaps();
			return texture;
		}
		// Preserve the transfer/validation failure while releasing storage allocated before that failure.
		catch { texture.Dispose(); throw; }
	}
}
