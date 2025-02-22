using System.Text;

namespace OpenTPW;

public static class Localization
{
	private static StringFile UIStrings;

	static Localization()
	{
		UIStrings = new StringFile( "Language/English/UITEXT.str" );
	}

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
					sb.Append( UIStrings[(int)enumVal] );
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
