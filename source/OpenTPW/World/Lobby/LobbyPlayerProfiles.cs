namespace OpenTPW;

/// <summary>An occupied slot: <c>Directory</c> is <c>users/&lt;slot+1&gt;&lt;Name&gt;</c> in the save filesystem.</summary>
public sealed record LobbyPlayerProfile( int Slot, string Name, string Directory );

/// <summary>The four login slots enumerated by TP.exe 0x5c2680.</summary>
public static class LobbyPlayerProfiles
{
	public const int SlotCount = 4;

	public static LobbyPlayerProfile?[] Read()
	{
		return FromDirectories( SaveFileSystem.DirectoryExists( "users" )
			? SaveFileSystem.GetDirectories( "users" ) : Array.Empty<string>() );
	}

	public static LobbyPlayerProfile?[] FromDirectories( IEnumerable<string> directories )
	{
		var slots = new LobbyPlayerProfile?[SlotCount];
		foreach ( string directory in directories )
		{
			string name = Path.GetFileName( directory.TrimEnd( '/', '\\' ) );
			// 0x5c2817..0x5c28d3 accepts a numeric prefix 1..4, strips exactly
			// that character, and assigns its suffix to that slot, including gaps.
			if ( name.Length < 2 || name[0] < '1' || name[0] > '4' ) continue;
			int slot = name[0] - '1';
			slots[slot] = new( slot, name[1..], directory );
		}
		return slots;
	}
}
