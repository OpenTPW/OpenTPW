using System.Text;

namespace OpenTPW;

public enum TextAlignment
{
	Left,
	Center,
	Right
}

/// <summary>Text coordinates are measured from the top of the original BF4 line box.</summary>
public sealed class TextLayout
{
	private const int SpacesPerTab = 4;
	private const float FallbackSpaceEm = .25f;
	public readonly record struct Quad( Rectangle Rectangle, Rectangle UV );

	public IReadOnlyList<Quad> Quads { get; }
	public Vector2 Size { get; }

	public TextLayout( string text,
		MsdfAtlas atlas,
		Bf4Metrics metrics,
		float emSize,
		Vector2 position,
		float scale = 1,
		TextAlignment alignment = TextAlignment.Left )
	{
		ArgumentNullException.ThrowIfNull( text );
		if ( !float.IsFinite( scale ) || scale <= 0 || !float.IsFinite( emSize ) || emSize <= 0 )
		{
			throw new ArgumentOutOfRangeException( nameof( scale ) );
		}

		var quads = new List<Quad>();
		var lines = text.Replace( "\r\n", "\n" ).Replace( '\r', '\n' ).Split( '\n' );
		float maxWidth = 0;
		float scaledEmSize = emSize * scale;
		for ( int line = 0; line < lines.Length; ++line )
		{
			int firstQuad = quads.Count;
			float advance = 0;
			int previousCodepoint = -1;
			foreach ( var rune in lines[line].EnumerateRunes() )
			{
				if ( rune.Value == '\t' )
				{
					float spaceWidth = metrics.Glyphs.TryGetValue( ' ', out var space ) ? space.Advance * scale : scaledEmSize * FallbackSpaceEm;
					float tabWidth = Math.Max( spaceWidth * SpacesPerTab, 1 );
					advance = (MathF.Floor( advance / tabWidth ) + 1) * tabWidth;
					previousCodepoint = -1;
					continue;
				}

				if ( !atlas.Glyphs.TryGetValue( rune.Value, out var glyph ) && !atlas.Glyphs.TryGetValue( '?', out glyph ) )
				{
					continue;
				}

				bool hasOriginalMetrics = metrics.Glyphs.TryGetValue( (uint)glyph.Codepoint, out var metric );
				// BF4 advances are already rounded by the original renderer; it applies no TTF kerning.
				if ( !hasOriginalMetrics && atlas.Kerning.TryGetValue( (previousCodepoint, glyph.Codepoint), out var adjustment ) )
				{
					advance += adjustment * scaledEmSize;
				}

				if ( glyph.Plane is { } plane && glyph.Atlas is { } atlasBounds )
				{
					var rect = new Rectangle( position.X + advance + plane.Left * scaledEmSize,
						position.Y + line * metrics.LineHeight * scale + (atlas.Ascender + plane.Top) * scaledEmSize,
						(plane.Right - plane.Left) * scaledEmSize,
						(plane.Bottom - plane.Top) * scaledEmSize );
					var uv = new Rectangle( atlasBounds.Left / atlas.Width,
						atlasBounds.Top / atlas.Height,
						(atlasBounds.Right - atlasBounds.Left) / atlas.Width,
						(atlasBounds.Bottom - atlasBounds.Top) / atlas.Height );
					quads.Add( new( rect, uv ) );
				}

				advance += hasOriginalMetrics ? metric.Advance * scale : glyph.Advance * scaledEmSize;
				previousCodepoint = glyph.Codepoint;
			}

			float shift = alignment switch
			{
				TextAlignment.Center => -advance / 2,
				TextAlignment.Right => -advance,
				_ => 0
			};
			for ( int i = firstQuad; i < quads.Count; ++i )
			{
				quads[i] = quads[i] with
				{
					Rectangle = quads[i].Rectangle.Shift( new Vector2( shift, 0 ) )
				};
			}

			maxWidth = Math.Max( maxWidth, advance );
		}

		Quads = quads;
		Size = text.Length == 0 ? Vector2.Zero : new Vector2( maxWidth, lines.Length * metrics.LineHeight * scale );
	}
}
