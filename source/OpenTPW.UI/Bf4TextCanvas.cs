namespace OpenTPW.UI;

/// <summary>
/// A CPU text surface prepared before GPU recording. Retail BF4 glyphs use their signed bearings directly
/// (Font_DrawGlyph, US 0x6b4aa0 / TPI 0x89b460); line advance is the header size
/// (Font_GetLineHeight, US 0x6b05e0 / TPI 0x896f50).
/// </summary>
public sealed class Bf4TextCanvas
{
	public TextureData Image { get; }
	public Bf4TextCanvas( int width, int height )
	{
		if ( width <= 0 || height <= 0 ) throw new ArgumentOutOfRangeException( nameof( width ) );
		Image = new TextureData( width, height, new byte[checked(width * height * 4)] );
	}

	/// <summary>Draws a laid-out block; the host supplies its pixel rectangle, state tint and optional shadow.</summary>
	public void Draw( FontFile font, string text, int left, int top, int width, int height,
		int horizontal, int vertical, uint color, bool wrap = false, byte shadowAlpha = 0 )
	{
		if ( (uint)horizontal > 2 || (uint)vertical > 2 ) throw new ArgumentOutOfRangeException( nameof( horizontal ) );
		string[] lines = Bf4TextLayout.Layout( text, wrap ? width : 0x4000, s => font.MeasureString( s ) );
		int maxWidth = lines.Length == 0 ? 0 : lines.Max( font.MeasureString );
		int blockX = left + (width - maxWidth) * horizontal / 2;
		int firstY = top + (height - lines.Length * font.SizeHint) * vertical / 2;
		// The complete shadow pass precedes the foreground, including when glyphs from adjacent lines overlap.
		for ( int pass = shadowAlpha == 0 ? 1 : 0; pass < 2; pass++ )
		{
			int y = firstY;
			foreach ( string line in lines )
			{
				int x = blockX + (maxWidth - font.MeasureString( line )) * horizontal / 2;
				if ( pass == 0 ) DrawLine( x - 2, y + 2, 128, shadowAlpha );
				else DrawLine( x, y, 255, 255 );
				y += font.SizeHint;

				void DrawLine( int pen, int baseline, byte level, byte opacity )
				{
					foreach ( char character in line )
					{
						var glyph = font.GetGlyph( character );
						if ( glyph == null ) continue;
						for ( int gy = 0; gy < glyph.Height; gy++ )
							for ( int gx = 0; gx < glyph.Width; gx++ )
							{
								int px = pen + glyph.OffsetX + gx, py = baseline + glyph.OffsetY + gy;
								if ( (uint)px >= Image.Width || (uint)py >= Image.Height ) continue;
								int alpha = glyph.Pixels[gy * glyph.Width + gx] * opacity / 255 * (int)(color & 255) / 255;
								if ( alpha == 0 ) continue;
								int offset = (py * Image.Width + px) * 4;
								int oldAlpha = Image.Data[offset + 3], retained = oldAlpha * (255 - alpha) / 255;
								int combined = alpha + retained;
								for ( int channel = 0; channel < 3; channel++ )
								{
									int value = (int)((color >> (24 - channel * 8)) & 255) * level / 255;
									Image.Data[offset + channel] = (byte)((value * alpha + Image.Data[offset + channel] * retained) / combined);
								}
								Image.Data[offset + 3] = (byte)combined;
							}
						pen += glyph.Advance;
					}
				}
			}
		}
	}
}
