namespace OpenTPW;

/// <summary>Lobby gate names from the US object-name table at 0x7488d8, shared by signs and island titles.</summary>
public static class LobbyObjectNames
{
	public static (string First, string Second) Lines( int gateObjectId )
	{
		int index = gateObjectId switch
		{
			1601 => 85,
			2601 => 179,
			4601 => 272,
			3601 => 368,
			_ => throw new InvalidDataException( $"Unknown lobby gate object {gateObjectId}" )
		};
		var names = new StringFile( GameLanguage.Path( "OBJECT_NAMES.str" ) );
		if ( index + 1 >= names.Entries.Length ) throw new InvalidDataException( $"Missing object-name lines for gate {gateObjectId}" );
		return (names[index], names[index + 1]);
	}

	/// <summary>US 0x4146b0 joins the two object-name lines, removing a trailing hyphen instead of inserting a space.</summary>
	public static string Title( string first, string second ) => first.EndsWith( "-", StringComparison.Ordinal )
		? first[..^1] + second : first + (second.Length == 0 ? "" : " ") + second;
}
