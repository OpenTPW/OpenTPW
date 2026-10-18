namespace OpenTPW;

/// <summary>Island ordering and asset names shared by the lobby and park loader.</summary>
public static class ParkThemes
{
	public sealed record Theme( string DirectoryName, string DisplayName, string LobbyName,
		string LobbyModelPrefix, Vector3 LobbyPosition, float LobbyFocusHeight, float LobbyHeading )
	{
		public Vector3 LobbyFocus => LobbyPosition + Vector3.Up * LobbyFocusHeight;
	}

	public static IReadOnlyList<Theme> All { get; } = Array.AsReadOnly( new Theme[]
	{
		new( "jungle", "LOST Kingdom", "Jungle", "Jun", new( 400, 400, 0 ), 12.5f, 90 ),
		new( "hallow", "HALLOWEEN world", "Hallow", "Hal", new( 600, 400, 0 ), 38, 180 ),
		new( "fantasy", "Wonder Land", "Fantasy", "Fan", new( 600, 600, 0 ), 17.5f, 270 ),
		new( "space", "Space Zone", "Space", "Spa", new( 400, 600, 0 ), 35, 0 )
	} );

	public static Theme Get( int island )
	{
		if ( island < 0 || island >= All.Count )
		{
			throw new ArgumentOutOfRangeException( nameof( island ) );
		}

		return All[island];
	}
}
