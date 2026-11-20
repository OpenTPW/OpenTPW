namespace OpenTPW.UI;

/// <summary>Shared BF4 paragraph splitting and word wrapping used by both retail games.</summary>
public static class Bf4TextLayout
{
	/// <summary>
	/// The retail text layout (Text_Layout (US 0x6ae0f0; TPI 0x8949b0)). Text_ClassifyCharacter (US 0x6ae930) classifies each character with FUN_006aeab0 (US 0x6aeab0): space and tab 0x12
	/// (blank, break opportunity), line feed 0x4 and carriage return 0xC (paragraph end), anything else 0x1 (so a
	/// comma is not a break). Text_SplitParagraphs (US 0x6aeb10; TPI 0x8953c0) ends a paragraph at each 0x4 character, swallowing one directly following
	/// carriage return, and emits a final paragraph only when characters remain. Text_WrapLines (US 0x6aec80; TPI 0x895530) fills lines: characters are
	/// added while the width stays within the line; on overflow it looks back from the overflowing character (not
	/// testing the line's first one) for a space or tab. If found, the line ends before it, minus earlier blanks; if
	/// not, the word is cut before the overflowing character, or after it when that character starts the line. Blanks
	/// after a break are skipped. Paragraph breaks have no width and are not drawn.
	/// </summary>
	public static string[] Layout( string text, float width, Func<string, float> measure )
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
}
