using System.Globalization;

namespace OpenTPW;

/// <summary>Normal local lobby selection and key checks (0x5dc5f0, 0x5dc6e0, 0x5dcb90).</summary>
public sealed class LobbyNavigation
{
	private readonly LobbyConfig config;
	private readonly Dictionary<int, int> costs = new();
	private readonly Dictionary<int, string> defaultTitles = new();
	private Dictionary<int, string> titles = new();
	private PlayerProfileFile? profile;
	private int index;
	public bool Active => profile != null;
	public bool Entering { get; private set; }
	public LobbyIslandConfig Island => config.Islands[index];
	/// <summary>Localized gate name or the selected player's saved sign name; prepared before the page is drawn.</summary>
	public string IslandTitle => Active ? titles[Island.Id] : defaultTitles[Island.Id];
	public int Keys => profile == null ? 0 : LobbyPlayerProgress.KeyCount( profile, config.Islands.Select( island => island.ThemeName ) );
	/// <summary>Keys needed to enter the current island: <c>Keys.CostToEnter</c> from the theme's global.sam (1..5).</summary>
	public int RequiredKeys => costs[Island.Id];
	/// <summary>
	/// Retail GameType 2 (US 0x550100 selects it when the profile's creation-mode byte +0x24, gms.dat prefix
	/// byte 14, is non-zero, i.e. Instant Action). The island page then hides the keys and disables the arrows
	/// (US 0x4b9340), next/previous return early (US 0x5e1ee0, 0x5e1f40) and entry skips the key check (US 0x5e1cc0).
	/// </summary>
	public bool InstantAction => profile != null && profile.CreationMode != 0;
	public bool CanEnter => Active && !Entering && (InstantAction || Keys >= RequiredKeys);
	public event Action<int>? IslandSelected;
	public event Action? EntryRequested;
	public event Action? EntryCancelled;
	public LobbyNavigation( LobbyConfig config )
	{
		this.config = config;
		foreach ( var island in config.Islands )
		{
			var settings = new SettingsFile( $"levels/{island.ThemeName}/global.sam" );
			if ( !int.TryParse( settings["Keys.CostToEnter"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cost ) || cost < 1 || cost > 5 )
				throw new InvalidDataException( $"Invalid Keys.CostToEnter for {island.ThemeName}" );
			costs.Add( island.Id, cost );
			// US 0x5b11c0 loads global.sam; its balance descriptor 0x73f8a8 puts ParkName.GateObjectId
			// at +0x18. US 0x5b12e0 passes that object ID to 0x412fb0 for the default title.
			if ( !int.TryParse( settings["ParkName.GateObjectId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int gateObjectId ) )
				throw new InvalidDataException( $"Invalid ParkName.GateObjectId for {island.ThemeName}" );
			var lines = LobbyObjectNames.Lines( gateObjectId );
			defaultTitles.Add( island.Id, LobbyObjectNames.Title( lines.First, lines.Second ) );
		}
	}
	public void Bind( PlayerProfileFile player )
	{
		ArgumentNullException.ThrowIfNull( player );
		titles = config.Islands.ToDictionary( island => island.Id,
			island => LobbyPlayerProgress.IslandTitle( player, island.ThemeName, defaultTitles[island.Id] ) );
		// A new player selection starts at the first configured island, never a stale entry state.
		profile = player;
		Entering = false;
		index = 0;
		IslandSelected?.Invoke( Island.Id );
	}
	/// <summary>Moves to the previous (-1, 0x5dc6e0) or next (+1, 0x5dc5f0) island, wrapping at both ends.</summary>
	public void Navigate( int direction )
	{
		if ( !Active || Entering || InstantAction ) return;
		// Normalize negative modulo as well: the previous-island control wraps at index zero.
		index = ((index + direction) % config.Islands.Count + config.Islands.Count) % config.Islands.Count;
		IslandSelected?.Invoke( Island.Id );
	}
	/// <summary>Starts the entry flight when enough keys are held (or in Instant Action); the gate opens once the camera is aligned.</summary>
	public bool Enter()
	{
		// Lock selection before notifying the camera so repeated input cannot queue two entries.
		if ( !CanEnter ) return false;
		Entering = true;
		EntryRequested?.Invoke();
		return true;
	}
	/// <summary>Cancels an entry in progress; the camera decides whether the opened gate closes (M2).</summary>
	public bool CancelEntry()
	{
		if ( !Entering ) return false;
		Entering = false;
		EntryCancelled?.Invoke();
		return true;
	}
	public void Unbind()
	{
		CancelEntry();
		profile = null;
	}
}
