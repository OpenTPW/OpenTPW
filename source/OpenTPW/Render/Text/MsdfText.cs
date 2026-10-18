using System.Numerics;
using Veldrid;

namespace OpenTPW;

/// <summary>Immediate UI text rendering. Call on the render thread inside a Graphics.Scope.</summary>
public static class MsdfText
{
	private static readonly Dictionary<string, Texture> Textures = new( StringComparer.OrdinalIgnoreCase );
	private static Material? _material;

	public static Vector2 Measure( string text, GameFont font, float scale = 1 )
	{
		var asset = MsdfFontCache.GetAtlas( font );
		return new TextLayout( text, asset.Atlas, MsdfFontCache.GetMetrics( font ), font.EmSize, Vector2.Zero, scale ).Size;
	}

	public static void Draw( string text, Vector2 position, GameFont font, RgbaFloat color,
		float scale = 1, TextAlignment alignment = TextAlignment.Left, float outlineWidth = 0, float horizontalScale = 1 )
	{
		if ( string.IsNullOrEmpty( text ) )
		{
			return;
		}

		var asset = MsdfFontCache.GetAtlas( font );
		var layout = new TextLayout( text, asset.Atlas, MsdfFontCache.GetMetrics( font ), font.EmSize, position, scale, alignment );
		if ( layout.Quads.Count == 0 )
		{
			return;
		}

		if ( !Textures.TryGetValue( font.FaceFile, out var texture ) )
		{
			texture = new Texture( asset.ImagePath, TextureFlags.DistanceField );
			if ( texture.Width != asset.Atlas.Width || texture.Height != asset.Atlas.Height )
			{
				throw new InvalidDataException( "MSDF image dimensions do not match the atlas metadata." );
			}

			Textures.Add( font.FaceFile, texture );
		}

		_material ??= new Material( Path.Combine( AppContext.BaseDirectory, "content", "shaders", "msdf.shader" ), MaterialFlags.DisableDepth );
		using var context = new ResourceContext();
		context.Set( "Color", texture );
		context.Set( "TextParameters", new Parameters
		{
			Color = new System.Numerics.Vector4( color.R, color.G, color.B, color.A ),
			Atlas = new System.Numerics.Vector4( asset.Atlas.DistanceRange, asset.Atlas.Width, asset.Atlas.Height, outlineWidth )
		} );
		IReadOnlyList<TextLayout.Quad> quads = layout.Quads;
		if ( horizontalScale != 1 )
		{
			// Stretch around the text origin so alignment and vertical layout stay intact.
			quads = layout.Quads.Select( quad => new TextLayout.Quad(
				new Rectangle( position.X + (quad.Rectangle.X - position.X) * horizontalScale,
					quad.Rectangle.Y, quad.Rectangle.Width * horizontalScale, quad.Rectangle.Height ), quad.UV ) ).ToArray();
		}

		Graphics.TextQuads( quads, _material );
	}

	private struct Parameters
	{
		public System.Numerics.Vector4 Color;
		public System.Numerics.Vector4 Atlas;
	}

	internal static void DisposeTextures()
	{
		// The renderer calls this only after waiting for the GPU at shutdown.
		foreach ( var texture in Textures.Values )
		{
			texture.NativeTextureView.Dispose();
			texture.NativeTexture.Dispose();
			Resource.All.Remove( texture );
		}

		Textures.Clear();
	}
}
