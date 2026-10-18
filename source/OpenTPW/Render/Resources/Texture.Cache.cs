namespace OpenTPW;

partial class Texture
{
	private TextureFlags creationFlags;

	private static bool TryGetCachedTexture( string path, TextureFlags flags, out Texture? texture )
	{
		texture = null;

		if ( string.IsNullOrEmpty( path ) )
			return false;

		// Filtering and chroma-key processing are part of an asset's identity.
		var existingTexture = Resource.All.OfType<Texture>().FirstOrDefault( t => t.Path == path && t.creationFlags == flags );
		if ( existingTexture != null )
		{
			texture = existingTexture;
			return true;
		}

		return false;
	}
}
