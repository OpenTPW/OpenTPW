using System.Text.Json;

namespace OpenTPW;

/// <summary>CPU-side MSDF atlas metadata, independent of the graphics device.</summary>
public sealed class MsdfAtlas
{
	public readonly record struct Bounds( float Left, float Top, float Right, float Bottom );

	public sealed record Glyph( int Codepoint, float Advance, Bounds? Plane, Bounds? Atlas );

	public int Width { get; }
	public int Height { get; }
	public float DistanceRange { get; }
	public float Ascender { get; }
	public float LineHeight { get; }
	public IReadOnlyDictionary<int, Glyph> Glyphs { get; }
	public IReadOnlyDictionary<(int, int), float> Kerning { get; }

	public MsdfAtlas( string json )
	{
		using var document = JsonDocument.Parse( json );
		var root = document.RootElement;
		var atlas = root.GetProperty( "atlas" );
		if ( atlas.GetProperty( "type" ).GetString() != "msdf" || atlas.GetProperty( "yOrigin" ).GetString() != "top" )
		{
			throw new InvalidDataException( "Expected an MSDF atlas with top-origin coordinates." );
		}

		Width = atlas.GetProperty( "width" ).GetInt32();
		Height = atlas.GetProperty( "height" ).GetInt32();
		DistanceRange = atlas.GetProperty( "distanceRange" ).GetSingle();
		var metrics = root.GetProperty( "metrics" );
		Ascender = -metrics.GetProperty( "ascender" ).GetSingle();
		LineHeight = metrics.GetProperty( "lineHeight" ).GetSingle();
		if ( Width <= 0 || Height <= 0 || DistanceRange <= 0 || LineHeight <= 0 )
		{
			throw new InvalidDataException( "Invalid MSDF atlas dimensions or metrics." );
		}

		var glyphs = new Dictionary<int, Glyph>();
		Bounds? ReadBounds( JsonElement glyph, string name ) => glyph.TryGetProperty( name, out var b ) ? new Bounds( b.GetProperty( "left" ).GetSingle(),
			b.GetProperty( "top" ).GetSingle(),
			b.GetProperty( "right" ).GetSingle(),
			b.GetProperty( "bottom" ).GetSingle() ) : null;
		foreach ( var glyph in root.GetProperty( "glyphs" ).EnumerateArray() )
		{
			int code = glyph.GetProperty( "unicode" ).GetInt32();
			glyphs.Add( code, new( code, glyph.GetProperty( "advance" ).GetSingle(), ReadBounds( glyph, "planeBounds" ), ReadBounds( glyph, "atlasBounds" ) ) );
		}

		Glyphs = glyphs;
		var kerning = new Dictionary<(int, int), float>();
		if ( root.TryGetProperty( "kerning", out var pairs ) )
		{
			foreach ( var pair in pairs.EnumerateArray() )
			{
				kerning[(pair.GetProperty( "unicode1" ).GetInt32(), pair.GetProperty( "unicode2" ).GetInt32())] = pair.GetProperty( "advance" ).GetSingle();
			}
		}

		Kerning = kerning;
	}
}
