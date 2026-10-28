using System.Text;

namespace OpenTPW;

/// <summary>Semantic UI strings across the 473-entry TW and 474-entry US language tables.</summary>
public static class Localization
{
	private static StringFile UIStrings;

	static Localization()
	{
		UIStrings = new StringFile( GameLanguage.Path( "UITEXT.str" ) );
	}

	public static int FileIndex( UIStrings key, int count ) => UiStringLayout.Index( key, count );

	public static string Text( UIStrings key ) => UIStrings[UiStringLayout.Index( key, UIStrings.Entries.Length )];
	/// <summary>Expands the existing #Name syntax through the selected table's layout.</summary>
	private class LocalizationParser : BaseParser
	{
		public LocalizationParser( string input ) : base( input ) { }

		public string Parse()
		{
			var sb = new StringBuilder();

			while ( !EndOfFile() )
			{
				sb.Append( ConsumeWhile( c => c != '#' ) );

				if ( !EndOfFile() )
				{
					ConsumeChar(); // #

					var key = ConsumeWhile( x => !char.IsWhiteSpace( x ) );
					key = key.Trim();

					var enumVal = Enum.Parse<UIStrings>( key );
					sb.Append( Text( enumVal ) );
				}
			}

			return sb.ToString();
		}
	}

	public static string Parse( string str )
	{
		var parser = new LocalizationParser( str );
		return parser.Parse();
	}
}

/// <summary>US inserts the unavailable-downloaded-ride message at 207 into the older 473-entry UI table.</summary>
internal static class UiStringLayout
{
	internal static int Index( UIStrings key, int count )
	{
		int index = (int)key;
		if ( count is not (473 or 474) ) throw new InvalidDataException( $"Unsupported UITEXT table layout: {count} entries" );
		if ( index < 0 || index >= 473 ) throw new ArgumentOutOfRangeException( nameof(key) );
		return count == 474 && index >= 207 ? index + 1 : index;
	}
}
