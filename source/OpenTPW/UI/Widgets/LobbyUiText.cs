using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// BF4 text in a retail 2048x1536 control rectangle (3.2 units per 640x480 pixel), clipped to that rectangle.
/// Alignment 0/1/2 = start/centre/end, as passed to 0x656a78.
/// </summary>
internal sealed class LobbyUiText : Panel
{
	private readonly FontFile font;
	private readonly Rectangle units;
	private readonly Dictionary<(string Text, uint Color), Texture> textures = new();
	public string Text { get; set; }
	public uint Color { get; set; }
	public Vector2 Offset { get; set; }
	public int HorizontalAlignment { get; set; } = 1;
	public int VerticalAlignment { get; set; } = 1;
	/// <summary>Horizontal scroll in 640x480 pixels, used by the name box to keep the caret visible.</summary>
	public float Scroll { get; set; }
	/// <summary>
	/// Draw as a UI-table text element (created by 0x48f7c0, drawn by 0x48f950): a shadow pass first.
	/// Menu items (0x490cd0) and edit boxes draw without it.
	/// </summary>
	public bool Shadow { get; set; }
	/// <summary>
	/// Lay the text out within the rectangle width. US 0x65c4b9(1, 1) sends text message 0x3000, which the text element
	/// handler 0x48fc40 stores at +0xda/+0xdb; the element draw 0x48f830 then passes its own width to the layout instead
	/// of the unlimited 0x4000 used when +0xda is 0. The confirmation message (0x47f0af) and the help strip (0x492233)
	/// set it. The break rules are in <see cref="Layout"/>.
	/// </summary>
	public bool WordWrap { get; set; }

	// 0x48f950 draws every text element twice: a shadow with texel (0x80, 0x80, 0x80, 0x80) 2 pixels
	// left and 2 down, then the text with (0xFF, 0xFF, 0xFF, 0xFF); both are tinted by the element colour.
	internal const int ShadowOffset = 2;
	private const byte ShadowLevel = 0x80;
	public float Measure( string text ) => font.MeasureString( text );
	public int LineHeight => font.LineHeight - font.Top;
	public LobbyUiText( string text, Rectangle units, string fontName = "GAME7", uint color = 0xffffffff )
	{
		font = new FontFile( GameLanguage.Path( $"{fontName}.bf4" ) );
		this.units = units;
		Text = text;
		Color = color;
	}
	/// <summary>RGBA image of a text element with its shadow (0x48f950); the text sits at (ShadowOffset, 0).</summary>
	internal static TextureData RenderElement( FontFile font, string text, uint color )
	{
		var mask = font.RenderString( text );
		int width = mask.Width + ShadowOffset, height = mask.Height + ShadowOffset;
		var data = new byte[width * height * 4];
		void Put( int x, int y, byte level, byte alpha )
		{
			int o = (y * width + x) * 4;
			data[o] = (byte)((color >> 24) * level / 255);
			data[o + 1] = (byte)(((color >> 16) & 0xFF) * level / 255);
			data[o + 2] = (byte)(((color >> 8) & 0xFF) * level / 255);
			data[o + 3] = (byte)((color & 0xFF) * alpha / 255);
		}
		for ( int pass = 0; pass < 2; pass++ )
			for ( int y = 0; y < mask.Height; y++ )
				for ( int x = 0; x < mask.Width; x++ )
				{
					var coverage = mask.Data[(y * mask.Width + x) * 4 + 3];
					if ( coverage == 0 ) continue;
					if ( pass == 0 ) Put( x, y + ShadowOffset, ShadowLevel, (byte)(ShadowLevel * coverage / 255) );
					else Put( x + ShadowOffset, y, 255, coverage );
				}
		return new TextureData( width, height, data );
	}

	protected override void OnRender()
	{
		if ( Text.Length == 0 ) return;
		var lines = Lines( Text );
		float top = units.Y / 3.2f + (units.Height / 3.2f - LineHeight * lines.Length) * VerticalAlignment / 2 + Offset.Y;
		for ( int line = 0; line < lines.Length; line++ ) DrawLine( lines[line], top + line * LineHeight );
	}
	/// <summary>Rasterize all possible option labels before interactive rendering starts.</summary>
	internal void Preload( IEnumerable<string> values )
	{
		foreach ( string value in values )
			foreach ( string line in Lines( value ) )
				if ( line.Length != 0 ) Prepare( line );
	}
	/// <summary>Lines as drawn: paragraphs, and with <see cref="WordWrap"/> lines filled to the rectangle width.</summary>
	internal string[] Lines( string text ) => Layout( text, WordWrap ? units.Width / 3.2f : float.PositiveInfinity, part => font.MeasureString( part ) );

	/// <summary>
	/// The retail text layout (US 0x6ae0f0). 0x6ae930 classifies each character with 0x6aeab0: space and tab 0x12
	/// (blank, break opportunity), line feed 0x4 and carriage return 0xC (paragraph end), anything else 0x1 (so a
	/// comma is not a break). 0x6aeb10 ends a paragraph at each 0x4 character, swallowing one directly following
	/// carriage return, and emits a final paragraph only when characters remain. 0x6aec80 fills lines: characters are
	/// added while the width stays within the line; on overflow it looks back from the overflowing character (not
	/// testing the line's first one) for a space or tab. If found, the line ends before it, minus earlier blanks; if
	/// not, the word is cut before the overflowing character, or after it when that character starts the line. Blanks
	/// after a break are skipped. Paragraph breaks have no width and are not drawn.
	/// </summary>
	internal static string[] Layout( string text, float width, Func<string, float> measure )
	{
		static bool Blank( char c ) => c is ' ' or '\t';
		static bool Break( char c ) => c is '\n' or '\r';
		static string Clean( string part ) => part.Replace( "\n", "" ).Replace( "\r", "" );
		var lines = new List<string>();
		void Fill( string raw )
		{
			int start = 0;
			for ( int k = 0; k < raw.Length; k++ )
			{
				if ( Break( raw[k] ) || measure( Clean( raw[start..(k + 1)] ) ) <= width ) continue;
				int end, next;
				if ( k == start ) end = next = k + 1;
				else
				{
					int blank = k;
					while ( blank != start && !Blank( raw[blank] ) ) blank--;
					if ( blank == start ) end = next = k;
					else
					{
						int last = blank - 1;
						while ( last != start && Blank( raw[last] ) ) last--;
						end = last + 1;
						next = blank;
					}
				}
				while ( next != raw.Length && Blank( raw[next] ) ) next++;
				lines.Add( Clean( raw[start..end] ) );
				start = next;
				k = next - 1;
			}
			if ( start != raw.Length ) lines.Add( Clean( raw[start..] ) );
		}
		int paragraph = 0;
		for ( int i = 0; i < text.Length; )
		{
			if ( !Break( text[i++] ) ) continue;
			if ( i < text.Length && text[i] == '\r' ) i++;
			Fill( text[paragraph..i] );
			paragraph = i;
		}
		if ( paragraph != text.Length ) Fill( text[paragraph..] );
		return lines.ToArray();
	}
	private Texture Prepare( string text )
	{
		if ( !textures.TryGetValue( (text, Color), out var texture ) )
		{
			var image = Shadow ? RenderElement( font, text, Color ) : font.RenderString( text, Color );
			textures.Add( (text, Color), texture = new Texture( image.Data, image.Width, image.Height, TextureFlags.PointFilter ) );
		}
		return texture;
	}
	private void DrawLine( string text, float y )
	{
		if ( text.Length == 0 ) return;
		var texture = Prepare( text );
		// The text itself keeps its place; a shadowed image starts 2 pixels further left.
		float x = units.X / 3.2f + (units.Width / 3.2f - font.MeasureString( text )) * HorizontalAlignment / 2 + Offset.X - Scroll
			- (Shadow ? ShadowOffset : 0);
		float left = Math.Max( x, units.X / 3.2f ), right = Math.Min( x + texture.Width, (units.X + units.Width) / 3.2f );
		if ( right <= left ) return;
		var material = Material.UI;
		material.Set( "Color", texture );
		Graphics.Quad( new Rectangle( left * 2, 720 - (y + texture.Height) * 1.5f, (right - left) * 2, texture.Height * 1.5f ),
			new Rectangle( (left - x) / texture.Width, 0, (right - left) / texture.Width, 1 ), material );
	}
}
