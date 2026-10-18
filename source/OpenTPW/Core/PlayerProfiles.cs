using System.Text.Json;

namespace OpenTPW;

public enum PlayerGameMode
{
	InstantAction,
	FullSimulation
}

public sealed record PlayerProfile( int Slot, string Name, PlayerGameMode Mode );

/// <summary>The original game names its four user directories 1Name through 4Name.</summary>
public sealed class PlayerProfiles
{
	public const int SlotCount = 4;
	private const string MetadataFileName = "opentpw-player.json";
	public const int MaxNameLength = 16;
	private readonly string usersPath;

	public PlayerProfiles( string savePath ) => usersPath = Path.Combine( savePath, "users" );

	public IReadOnlyList<PlayerProfile> Load()
	{
		if ( !Directory.Exists( usersPath ) )
		{
			return Array.Empty<PlayerProfile>();
		}

		var players = new List<PlayerProfile>();
		foreach ( var directory in Directory.GetDirectories( usersPath ).OrderBy( x => x, StringComparer.OrdinalIgnoreCase ) )
		{
			var name = Path.GetFileName( directory );
			if ( name.Length < 2 || name[0] < '1' || name[0] >= '1' + SlotCount )
			{
				continue;
			}

			int slot = name[0] - '1';
			if ( players.Any( p => p.Slot == slot ) )
			{
				continue;
			}

			var mode = ReadMode( directory );
			players.Add( new PlayerProfile( slot, name[1..], mode ) );
		}

		return players;
	}

	private static PlayerGameMode ReadMode( string directory )
	{
		var metadata = Path.Combine( directory, MetadataFileName );
		if ( File.Exists( metadata ) )
		{
			try
			{
				return JsonSerializer.Deserialize<PlayerProfile>( File.ReadAllText( metadata ) )?.Mode ?? PlayerGameMode.FullSimulation;
			}
			catch ( JsonException )
			{
				// Original profiles need no OpenTPW metadata.
			}
		}

		return PlayerGameMode.FullSimulation;
	}

	public PlayerProfile Create( int slot, string name, PlayerGameMode mode )
	{
		name = name.Trim();
		if ( slot < 0 || slot >= SlotCount )
		{
			throw new ArgumentOutOfRangeException( nameof( slot ) );
		}

		if ( name.Length == 0 || name.Length > MaxNameLength || name.EndsWith( '.' ) ||
			name.Any( c => char.IsControl( c ) || "<>:\"/\\|?*".Contains( c ) ) )
		{
			throw new ArgumentException( "Enter a name of 1–16 characters without filename symbols." );
		}

		var players = Load();
		if ( players.Any( p => p.Slot == slot ) )
		{
			throw new InvalidOperationException( "This player slot is already occupied." );
		}

		if ( players.Any( p => string.Equals( p.Name, name, StringComparison.OrdinalIgnoreCase ) ) )
		{
			throw new ArgumentException( "A player with that name already exists. Choose a different name." );
		}

		var profile = new PlayerProfile( slot, name, mode );
		var directory = Path.Combine( usersPath, $"{slot + 1}{name}" );
		// Publish a complete directory so a failed write cannot leave a ghost slot.
		Directory.CreateDirectory( usersPath );
		var staging = Path.Combine( usersPath, ".opentpw-" + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( staging );
		try
		{
			using ( var stream = new FileStream( Path.Combine( staging, MetadataFileName ), FileMode.CreateNew, FileAccess.Write ) )
			{
				JsonSerializer.Serialize( stream, profile );
			}

			Directory.Move( staging, directory );
		}
		finally
		{
			if ( Directory.Exists( staging ) )
			{
				File.Delete( Path.Combine( staging, MetadataFileName ) );
				Directory.Delete( staging );
			}
		}

		return profile;
	}
}
