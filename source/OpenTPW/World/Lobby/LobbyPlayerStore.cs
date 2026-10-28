namespace OpenTPW;

/// <summary>Player directory creation and gms.dat loading (0x5c3030/0x5c3280/0x5c34a0).</summary>
public sealed class LobbyPlayerStore
{
	private readonly string users;
	private readonly BaseFileSystem data;
	private readonly string[] themes;
	public LobbyPlayerStore( string usersDirectory, BaseFileSystem data, IEnumerable<string> themes )
	{
		users = Path.GetFullPath( usersDirectory );
		this.data = data;
		this.themes = themes.ToArray();
		if ( this.themes.Any( theme => string.IsNullOrEmpty( theme ) || theme.IndexOfAny( Path.GetInvalidFileNameChars() ) >= 0 || theme is "." or ".." ) )
			throw new ArgumentException( "Invalid player theme directory" );
	}
	public LobbyPlayerProfile?[] ReadSlots() => LobbyPlayerProfiles.FromDirectories(
		Directory.Exists( users ) ? Directory.GetDirectories( users ) : [] );
	public PlayerProfileFile Load( LobbyPlayerProfile profile )
	{
		string expected = Path.Combine( users, $"{profile.Slot + 1}{profile.Name}" );
		if ( !Path.GetFullPath( profile.Directory ).Equals( expected, StringComparison.OrdinalIgnoreCase ) )
			throw new InvalidDataException( "Profile does not belong to this player directory" );
		return PlayerProfileFile.Read( File.ReadAllBytes( Path.Combine( expected, "gms.dat" ) ) );
	}

	public LobbyPlayerProfile Create( int slot, string name, byte creationMode, byte[]? preferences = null )
	{
		var flow = new LobbyPlayerFlow( new LobbyPlayerProfile?[4] );
		if ( !flow.Select( slot ) ) throw new ArgumentOutOfRangeException( nameof(slot) );
		flow.Name = name;
		if ( !flow.CanConfirm || flow.ResolveName() != name ) throw new ArgumentException( "Invalid player name", nameof(name) );
		if ( ReadSlots()[slot] != null ) throw new IOException( "Player slot is already occupied" );
		// A new player inherits accepted global defaults; existing profiles retain their own preferences.
		var profileData = PlayerProfileFile.Create( creationMode, preferences ?? PlayerProfileFile.DefaultPreferences( new SettingsFile( data.OpenRead( "sound.sam" ) ) ) );
		// Normal lobby creation grants one key before saving (0x4a44e0 -> 0x5ac050 -> 0x5c3b00).
		// Keep this inside the staged transaction so retries cannot grant another key to an occupied slot.
		LobbyPlayerProgress.GrantIntroductionKey( profileData );
		string destination = Path.Combine( users, $"{slot + 1}{name}" );
		Directory.CreateDirectory( users );
		string staging = Path.Combine( users, ".create-" + Guid.NewGuid().ToString( "N" ) );
		try
		{
			Directory.CreateDirectory( staging );
			foreach ( string theme in themes )
			{
				string directory = Path.Combine( staging, theme );
				Directory.CreateDirectory( directory );
				// 0x5c3280 copies only templates that actually exist; only jungle ships one in TW.
				string template = $"levels/{theme}/easymode.TPWI";
				if ( creationMode == 0 || !data.FileExists( template ) ) continue;
				File.WriteAllBytes( Path.Combine( directory, "easymode.TPWI" ), data.ReadAllBytes( template ) );
			}
			File.WriteAllBytes( Path.Combine( staging, "gms.dat" ), profileData.Write() );
			if ( ReadSlots()[slot] != null ) throw new IOException( "Player slot became occupied during creation" );
			Directory.Move( staging, destination );
		}
		finally
		{
			string resolved = Path.GetFullPath( staging );
			if ( resolved.StartsWith( users.TrimEnd( Path.DirectorySeparatorChar ) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase ) &&
				Path.GetFileName( resolved ).StartsWith( ".create-", StringComparison.Ordinal ) && Directory.Exists( resolved ) ) Directory.Delete( resolved, true );
		}
		return new( slot, name, destination );
	}
}
