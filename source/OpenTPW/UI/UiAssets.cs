namespace OpenTPW.UI;

internal enum UiModelPlane { XY, XZ }

/// <summary>Shared immutable UI assets. Load and use them on the render thread.</summary>
internal static class UiAssets
{
	private readonly record struct ModelKey( string Name, float Width, float Height, UiModelPlane Plane, bool FirstMeshOnly, bool FlipVertical );
	private static readonly Dictionary<(string Name, TextureFlags Flags), Texture> Textures = new();
	private static readonly Dictionary<ModelKey, Model> Models = new();

	public static Texture GetTexture( string name, TextureFlags flags = TextureFlags.None )
	{
		var key = (name, flags);
		if ( !Textures.TryGetValue( key, out var texture ) )
		{
			texture = new Texture( $"ui/textures/{name}.wct", flags );
			Textures.Add( key, texture );
		}

		return texture;
	}

	public static Model GetModel( string name, Vector2 size, UiModelPlane plane = UiModelPlane.XY,
		bool firstMeshOnly = false, bool flipVertical = true )
	{
		// Projection and mesh state are part of the key: the same MD2 can be
		// used differently by two panels without sharing incompatible geometry.
		var key = new ModelKey( name, size.X, size.Y, plane, firstMeshOnly, flipVertical );
		if ( !Models.TryGetValue( key, out var model ) )
		{
			model = UiModel.Load( $"ui/{name}.md2", size,
				point => new Vector2( point.X, plane == UiModelPlane.XZ ? point.Z : point.Y ),
				firstMeshOnly, flipVertical );
			Models.Add( key, model );
		}

		return model;
	}
}
