namespace OpenTPW;

partial class Texture
{
	private TextureFlags cacheFlags;
	private static bool TryGetCachedTexture( string path, TextureFlags flags, out Texture? texture )
	{
		texture = null;

		if ( string.IsNullOrEmpty( path ) )
			return false;

		// Chroma-key preprocessing and sampler selection are part of texture identity.
		// Reusing only by path makes the first load's options leak into later callers.
		var existingTexture = Asset.All.OfType<Texture>().FirstOrDefault( t => t.Path == path && t.cacheFlags == flags );
		if ( existingTexture != null )
		{
			texture = existingTexture;
			return true;
		}

		return false;
	}
}
