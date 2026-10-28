using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>Lobby key accounting at 0x5abaa0 and the new-player grant at 0x4a44e0/0x5ac050.</summary>
public static class LobbyPlayerProgress
{
	/// <summary>
	/// US 0x5b12e0 uses the saved sign lines only when mNameChanged is set. US 0x5b0d70 serializes
	/// two interleaved arrays of 33 UTF-16 code units at segment byte 26, then mNameChanged at byte 158.
	/// </summary>
	public static string IslandTitle( PlayerProfileFile profile, string theme, string defaultTitle )
	{
		var segment = profile.Segments.FirstOrDefault( item => item.Name.TrimEnd( '\0' ).Equals( theme, StringComparison.OrdinalIgnoreCase ) );
		if ( segment == null ) return defaultTitle;
		if ( segment.Data.Length != 160 ) throw new InvalidDataException( $"Invalid player segment for {theme}" );
		if ( segment.Data[158] == 0 ) return defaultTitle;
		string Line( int offset )
		{
			var characters = new char[33];
			for ( int i = 0; i < characters.Length; i++ ) characters[i] = (char)BinaryPrimitives.ReadUInt16LittleEndian( segment.Data.AsSpan( offset + i * 4, 2 ) );
			int end = Array.IndexOf( characters, '\0' );
			if ( end < 0 ) throw new InvalidDataException( $"Unterminated player sign name for {theme}" );
			return new string( characters, 0, end );
		}
		string first = Line( 26 ), second = Line( 28 );
		// US 0x5b12e0 handles an empty first line before the shared hyphen/space rule.
		return first.Length == 0 ? second : LobbyObjectNames.Title( first, second );
	}

	public static int KeyCount( PlayerProfileFile profile, IEnumerable<string> themes )
	{
		var available = themes.ToHashSet( StringComparer.OrdinalIgnoreCase );
		int awards = profile.Prefix.Take( 6 ).Count( value => value != 0 );
		foreach ( var segment in profile.Segments )
			if ( available.Contains( segment.Name.TrimEnd( '\0' ) ) ) awards += segment.Data.Take( 6 ).Count( value => value != 0 );
		return checked(awards / 3 + BinaryPrimitives.ReadInt32LittleEndian( profile.Prefix.AsSpan( 10, 4 ) ));
	}
	public static void GrantIntroductionKey( PlayerProfileFile profile )
	{
		var field = profile.Prefix.AsSpan( 10, 4 );
		BinaryPrimitives.WriteInt32LittleEndian( field, checked(BinaryPrimitives.ReadInt32LittleEndian( field ) + 1) );
	}
}
