namespace OpenTPW;

public class SAMParser : BaseParser
{
	public SAMParser( string input ) : base( input ) { }

	public List<SettingsPair> Parse()
	{
		var settings = new List<SettingsPair>();

		while ( !EndOfFile() )
		{
			ConsumeWhitespace();

			if ( EndOfFile() )
				break;

			if ( NextChar() == '#' )
			{
				ConsumeComment();
			}
			else
			{
				var pair = ParsePair();
				if ( !string.IsNullOrEmpty( pair.Key ) && !string.IsNullOrEmpty( pair.Value ) )
				{
					settings.Add( pair );
				}
			}
		}

		return settings;
	}

	protected void ConsumeComment()
	{
		ConsumeWhile( c => c != '\n' );
	}

	protected SettingsPair ParsePair()
	{
		var key = ConsumeWhile( c => !char.IsWhiteSpace( c ) );
		ConsumeWhitespace();
		var value = ConsumeWhile( c => !char.IsWhiteSpace( c ) && c != '#' );
		ConsumeWhile( c => c != '\r' && c != '\n' );

		return new SettingsPair { Key = key.Trim(), Value = value.Trim() };
	}
}
