using Veldrid;

namespace OpenTPW.UI;

/// <summary>The shared top-right key display in the lobby and park HUD.</summary>
internal static class KeyCounter
{
	// Initial status placeholder until persistent key progression is implemented.
	public const int InitialCount = 3;
	// Reference-screen pixels; X coordinates are distances from the right edge.
	private static readonly Rectangle IconBounds = new( 69, 24, 38, 44 );
	private static readonly Vector2 CountPosition = new( 107, 36 );
	private const float TextScale = .7f;

	public static void Draw( int count, float scale, TextureFlags flags = TextureFlags.None )
	{
		UiDrawing.Sprite( "gkey3", new Rectangle( Screen.RawSize.X - IconBounds.X * scale,
			IconBounds.Y * scale, IconBounds.Width * scale, IconBounds.Height * scale ), flags );
		MsdfText.Draw( $"{count} x", new Vector2( Screen.RawSize.X - CountPosition.X * scale,
			CountPosition.Y * scale ), GameFont.MenuSmall, RgbaFloat.White, scale * TextScale );
	}
}
