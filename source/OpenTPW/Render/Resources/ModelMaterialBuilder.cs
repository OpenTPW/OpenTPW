namespace OpenTPW;

/// <summary>Build batch materials and reuse resolved textures within one model.</summary>
internal sealed class ModelMaterialBuilder
{
	private readonly string modelPath;
	private readonly Material baseMaterial;
	private readonly TextureFlags textureFlags;
	private readonly Func<string, Texture?>? textureOverride;
	private readonly bool warnOnMissing;
	private readonly Dictionary<string, Texture> texturesByName = new( StringComparer.OrdinalIgnoreCase );
	private Texture? fallback;
	private Texture Fallback => fallback ??= Texture.Missing;

	public ModelMaterialBuilder( string modelPath, Material baseMaterial, TextureFlags textureFlags = TextureFlags.None,
		Func<string, Texture?>? textureOverride = null, bool warnOnMissing = false )
	{
		this.modelPath = modelPath;
		this.baseMaterial = baseMaterial;
		this.textureFlags = textureFlags;
		this.textureOverride = textureOverride;
		this.warnOnMissing = warnOnMissing;
	}

	public Material Create( ModelFile.MaterialData[] materials )
	{
		var material = baseMaterial.Copy();
		var textures = Enumerable.Repeat( Fallback, ModelGeometry.TextureSlotCount ).ToArray();
		for ( int i = 0; i < materials.Length; i++ )
		{
			var source = materials[i];
			// Null MD2 references have no texture path; keep the neutral fallback.
			if ( source.HasTextureReference && !string.IsNullOrEmpty( source.Name ) )
			{
				textures[i] = ResolveTexture( source.Name );
			}
		}

		material.Set( "Color", textures );
		return material;
	}

	private Texture ResolveTexture( string name )
	{
		if ( texturesByName.TryGetValue( name, out var texture ) )
		{
			return texture;
		}

		texture = textureOverride?.Invoke( name );
		if ( texture == null )
		{
			string? path = ModelTextureResolver.FindPath( modelPath, name, FileSystem.FileExists );
			if ( path == null && warnOnMissing )
			{
				Log.Warning( $"Model {modelPath}: texture {name} was not found locally or in the park's shared textures." );
			}

			texture = path == null ? Fallback : new Texture( path, textureFlags );
		}

		texturesByName.Add( name, texture );
		return texture;
	}
}
