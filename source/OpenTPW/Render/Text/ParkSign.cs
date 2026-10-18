using StbImageSharp;

namespace OpenTPW;

/// <summary>Render one wide board, then split it into the gate model's SIGN1/SIGN2 textures.</summary>
public static class ParkSign
{
	// Reconstructed framing, rather than recovered GDI layout. Change these to
	// adjust the lettering; the dimensions required by the model live in SignFile.
	private const int SingleLineTop = 36;
	private const int FirstLineTop = 6;
	private const int SecondLineTop = 68;
	private const float HorizontalMargin = 6;
	private const float MaximumLineHeight = 52;
	private const float FontHeightToPixels = 0.5f;
	private const int AtlasEmSize = 48;
	private const float BevelStrengthDivisor = 20;
	private const float MaximumBevelShade = 0.3f;
	private const int BoardWidth = SignFile.BoardWidth;
	private const int BoardHeight = SignFile.BoardHeight;
	private const int Channels = SignFile.ColorChannels;

	private sealed record LineLayout( List<(MsdfAtlas.Glyph Glyph, float X)> Glyphs,
		float Scale, float OriginX, float OriginY, float Height );

	public static (string First, string Second) SplitName( string name )
	{
		ArgumentNullException.ThrowIfNull( name );
		// Original routine 00467B41 prefers the left space when two candidates
		// are equally far from the centre of the name.
		int middle = name.Length / 2;
		for ( int distance = 0; distance <= name.Length; distance++ )
		{
			int left = middle - distance;
			int right = middle + distance;
			if ( left >= 0 && left < name.Length && name[left] == ' ' )
			{
				return (name[..left], name[(left + 1)..]);
			}

			if ( right < name.Length && name[right] == ' ' )
			{
				return (name[..right], name[(right + 1)..]);
			}
		}

		return (name, "");
	}

	public static (TextureData First, TextureData Second) Render( SignFile sign, string name )
	{
		var (firstLine, secondLine) = SplitName( name );
		byte[] board = (byte[])sign.Background.Data.Clone();
		DrawLine( board, sign.Styles[0], firstLine, secondLine.Length == 0 ? SingleLineTop : FirstLineTop );
		DrawLine( board, sign.Styles[1], secondLine, SecondLineTop );
		return SplitBoard( board );
	}

	private static (TextureData First, TextureData Second) SplitBoard( byte[] board )
	{
		const int halfWidth = SignFile.TextureSize;
		const int halfRowBytes = halfWidth * Channels;
		var first = new byte[halfWidth * BoardHeight * Channels];
		var second = new byte[first.Length];
		for ( int y = 0; y < BoardHeight; y++ )
		{
			int sourceRow = y * BoardWidth * Channels;
			int destinationRow = y * halfRowBytes;
			Array.Copy( board, sourceRow, first, destinationRow, halfRowBytes );
			Array.Copy( board, sourceRow + halfRowBytes, second, destinationRow, halfRowBytes );
		}

		return (new( halfWidth, BoardHeight, first ), new( halfWidth, BoardHeight, second ));
	}

	private static void DrawLine( byte[] board, SignFile.TextStyle style, string text, int top )
	{
		if ( text.Length == 0 )
		{
			return;
		}

		var asset = MsdfFontCache.GetAtlas( new GameFont( "", style.FontFile, AtlasEmSize, true ) );
		var layout = LayoutLine( asset.Atlas, style, text, top );
		if ( layout == null )
		{
			return;
		}

		var image = ImageResult.FromMemory( File.ReadAllBytes( asset.ImagePath ), ColorComponents.RedGreenBlueAlpha );
		var coverage = RenderCoverage( asset.Atlas, image, layout );
		DrawShadow( board, coverage, style );
		DrawGradient( board, coverage, style, top, layout.Height );
	}

	private static LineLayout? LayoutLine( MsdfAtlas atlas, SignFile.TextStyle style, string text, int top )
	{
		var glyphs = new List<(MsdfAtlas.Glyph Glyph, float X)>();
		float advance = 0;
		float minX = float.MaxValue, maxX = float.MinValue;
		float minY = float.MaxValue, maxY = float.MinValue;
		foreach ( var rune in text.EnumerateRunes() )
		{
			if ( !atlas.Glyphs.TryGetValue( rune.Value, out var glyph ) && !atlas.Glyphs.TryGetValue( '?', out glyph ) )
			{
				continue;
			}

			glyphs.Add( (glyph, advance) );
			if ( glyph.Plane is { } plane )
			{
				minX = Math.Min( minX, advance + plane.Left );
				maxX = Math.Max( maxX, advance + plane.Right );
				minY = Math.Min( minY, plane.Top );
				maxY = Math.Max( maxY, plane.Bottom );
			}

			// SGN spacing is a percentage of each glyph's normal advance.
			advance += glyph.Advance * style.Spacing / 100f;
		}

		if ( maxX <= minX || maxY <= minY )
		{
			return null;
		}

		float availableWidth = BoardWidth - HorizontalMargin * 2;
		float scale = Math.Min( Math.Abs( style.Height ) * FontHeightToPixels,
			Math.Min( availableWidth / (maxX - minX), MaximumLineHeight / (maxY - minY) ) );
		float originX = (BoardWidth - (maxX - minX) * scale) / 2 - minX * scale;
		float originY = top - minY * scale;
		return new LineLayout( glyphs, scale, originX, originY, (maxY - minY) * scale );
	}

	private static float[] RenderCoverage( MsdfAtlas atlas, ImageResult image, LineLayout layout )
	{
		var coverage = new float[BoardWidth * BoardHeight];
		foreach ( var (glyph, advance) in layout.Glyphs )
		{
			if ( glyph.Plane is not { } plane || glyph.Atlas is not { } bounds )
			{
				continue;
			}

			float left = layout.OriginX + (advance + plane.Left) * layout.Scale;
			float top = layout.OriginY + plane.Top * layout.Scale;
			float width = (plane.Right - plane.Left) * layout.Scale;
			float height = (plane.Bottom - plane.Top) * layout.Scale;
			int startX = Math.Max( 0, (int)Math.Floor( left ) );
			int startY = Math.Max( 0, (int)Math.Floor( top ) );
			int endX = Math.Min( BoardWidth, (int)Math.Ceiling( left + width ) );
			int endY = Math.Min( BoardHeight, (int)Math.Ceiling( top + height ) );
			for ( int y = startY; y < endY; y++ )
			{
				for ( int x = startX; x < endX; x++ )
				{
					// Sample pixel centres. MSDF's glyph edge is at a median of 0.5.
					float u = bounds.Left + (x + .5f - left) / width * (bounds.Right - bounds.Left);
					float v = bounds.Top + (y + .5f - top) / height * (bounds.Bottom - bounds.Top);
					float red = Sample( image, u, v, 0 );
					float green = Sample( image, u, v, 1 );
					float blue = Sample( image, u, v, 2 );
					float median = Math.Max( Math.Min( red, green ), Math.Min( Math.Max( red, green ), blue ) );
					float opacity = Math.Clamp( (median - .5f) * atlas.DistanceRange * width / (bounds.Right - bounds.Left) + .5f, 0, 1 );
					int pixel = y * BoardWidth + x;
					coverage[pixel] = Math.Max( coverage[pixel], opacity );
				}
			}
		}

		return coverage;
	}

	private static void DrawShadow( byte[] board, float[] coverage, SignFile.TextStyle style )
	{
		int offsetX = style.ShadowOffsetX;
		int offsetY = style.ShadowOffsetY;
		for ( int y = 0; y < BoardHeight; y++ )
		{
			for ( int x = 0; x < BoardWidth; x++ )
			{
				int sourceX = x - offsetX;
				int sourceY = y - offsetY;
				if ( sourceX >= 0 && sourceX < BoardWidth && sourceY >= 0 && sourceY < BoardHeight )
				{
					float alpha = coverage[sourceY * BoardWidth + sourceX] * style.Effect[3] / byte.MaxValue;
					Blend( board, x, y, style.Effect[0], style.Effect[1], style.Effect[2], alpha );
				}
			}
		}
	}

	private static void DrawGradient( byte[] board, float[] coverage, SignFile.TextStyle style, int top, float lineHeight )
	{
		for ( int y = 0; y < BoardHeight; y++ )
		{
			for ( int x = 0; x < BoardWidth; x++ )
			{
				float opacity = coverage[y * BoardWidth + x];
				if ( opacity == 0 )
				{
					continue;
				}

				// SGN ramps use ARGB byte order. Sample their middle column along
				// the lettering height, then approximate the original bevel effect.
				int rampY = Math.Clamp( (int)((y - top) * (SignFile.GradientHeight - 1) / Math.Max( 1, lineHeight )), 0, SignFile.GradientHeight - 1 );
				int rampPixel = (rampY * SignFile.GradientWidth + SignFile.GradientWidth / 2) * Channels;
				float shade = y > 0 ? (coverage[(y - 1) * BoardWidth + x] - opacity) * Math.Min( style.Bevel / BevelStrengthDivisor, MaximumBevelShade ) : 0;
				byte Color( int channel ) => (byte)Math.Clamp( style.Gradient[rampPixel + channel] + shade * byte.MaxValue, 0, byte.MaxValue );
				Blend( board, x, y, Color( 1 ), Color( 2 ), Color( 3 ), opacity );
			}
		}
	}

	private static float Sample( ImageResult image, float u, float v, int channel )
	{
		u = Math.Clamp( u - .5f, 0, image.Width - 1 );
		v = Math.Clamp( v - .5f, 0, image.Height - 1 );
		int x = (int)u, y = (int)v;
		int nextX = Math.Min( x + 1, image.Width - 1 );
		int nextY = Math.Min( y + 1, image.Height - 1 );
		float Pixel( int px, int py ) => image.Data[(py * image.Width + px) * Channels + channel] / (float)byte.MaxValue;
		float first = Pixel( x, y ) + (Pixel( nextX, y ) - Pixel( x, y )) * (u - x);
		float second = Pixel( x, nextY ) + (Pixel( nextX, nextY ) - Pixel( x, nextY )) * (u - x);
		return first + (second - first) * (v - y);
	}

	private static void Blend( byte[] pixels, int x, int y, byte red, byte green, byte blue, float alpha )
	{
		int pixel = (y * BoardWidth + x) * Channels;
		pixels[pixel] = (byte)(pixels[pixel] * (1 - alpha) + red * alpha);
		pixels[pixel + 1] = (byte)(pixels[pixel + 1] * (1 - alpha) + green * alpha);
		pixels[pixel + 2] = (byte)(pixels[pixel + 2] * (1 - alpha) + blue * alpha);
		pixels[pixel + 3] = (byte)(pixels[pixel + 3] * (1 - alpha) + byte.MaxValue * alpha);
	}
}
