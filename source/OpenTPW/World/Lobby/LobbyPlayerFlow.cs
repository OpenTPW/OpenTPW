namespace OpenTPW;

/// <summary>Player page state; see the members.</summary>
public enum LobbyPlayerFlowState
{
	/// <summary>The four slot buttons are active and nothing is open.</summary>
	Browsing,

	/// <summary>The new-player name dialog is open for <see cref="LobbyPlayerFlow.ActiveSlot"/>.</summary>
	Naming,

	/// <summary>A slot was chosen; <see cref="LobbyPlayerFlow.Result"/> says which and why.</summary>
	Committed
}

/// <summary>Whether the committed slot loads an existing profile or creates one.</summary>
public enum LobbyPlayerChoiceKind
{
	None,
	Existing,
	New
}

/// <summary>The player the lobby settled on, for the controller to load or create.</summary>
public sealed record LobbyPlayerChoice( LobbyPlayerChoiceKind Kind, int Slot, string Name )
{
	public static readonly LobbyPlayerChoice None = new( LobbyPlayerChoiceKind.None, -1, "" );
}

/// <summary>
/// The lobby's player selection: choose an occupied slot, name a new player in an empty slot, or back out.
/// Pure state only; it reads nothing and writes no save. The four slots come from
/// <see cref="LobbyPlayerProfiles"/> (0x5c2680) and the controller wires the buttons.
/// </summary>
public sealed class LobbyPlayerFlow
{
	public const int SlotCount = LobbyPlayerProfiles.SlotCount;

	// 0x6607e8 stores a capacity of 0x10; 0x660977 indexes the buffer in 16-bit units.
	public const int MaxNameLength = 16;

	// 0x661cfb( 0x74e090 ) gives the name box the characters "\/*?:|<>"" it refuses to accept.
	private static readonly char[] ForbiddenCharacters = { '\\', '/', '*', '?', ':', '|', '<', '>', '"' };

	private readonly LobbyPlayerProfile?[] slots;

	public LobbyPlayerFlow( IEnumerable<LobbyPlayerProfile?> slots )
	{
		this.slots = ValidateSlots( slots );
	}

	/// <summary>Four slots from the save directory.</summary>
	public static LobbyPlayerFlow Read() => new( LobbyPlayerProfiles.Read() );

	public LobbyPlayerFlowState State { get; private set; } = LobbyPlayerFlowState.Browsing;

	/// <summary>The slot the name dialog belongs to, or -1 outside <see cref="LobbyPlayerFlowState.Naming"/>.</summary>
	public int ActiveSlot { get; private set; } = -1;

	/// <summary>The name box contents. Retail never clears 0x7e3bc0, so this survives a cancel.</summary>
	public string Name
	{
		get => name;
		set => name = Filter( value );
	}
	private string name = "";

	/// <summary>The pending choice, present only in the Committed state.</summary>
	public LobbyPlayerChoice? Result { get; private set; }

	public LobbyPlayerProfile? this[int slot] => IsValidSlot( slot ) ? slots[slot] : null;

	public string? SlotLabel( int slot ) => this[slot]?.Name;

	/// <summary>The name as it would be written: trailing spaces removed, everything else kept (0x4a4600).</summary>
	public string ResolveName() => Name.TrimEnd( ' ' );

	public bool CanConfirm => State == LobbyPlayerFlowState.Naming && ResolveName().Length > 0;

	/// <summary>Clicking a slot button (0x4a3a90 message 0x10006).</summary>
	public bool Select( int slot )
	{
		if ( State != LobbyPlayerFlowState.Browsing || !IsValidSlot( slot ) )
			return false;

		var profile = slots[slot];
		if ( profile != null )
		{
			Result = new( LobbyPlayerChoiceKind.Existing, slot, profile.Name );
			State = LobbyPlayerFlowState.Committed;
			return true;
		}

		ActiveSlot = slot;
		State = LobbyPlayerFlowState.Naming;
		return true;
	}

	/// <summary>Confirming the name dialog (0x4a4780 value 0xffffffff). An unusable name keeps the dialog open.</summary>
	public bool Confirm()
	{
		if ( !CanConfirm )
			return false;

		name = ResolveName();
		Result = new( LobbyPlayerChoiceKind.New, ActiveSlot, name );
		ActiveSlot = -1;
		State = LobbyPlayerFlowState.Committed;
		return true;
	}

	/// <summary>Cancelling the name dialog (0x4a4780 value 0xfffffffe): back to the buttons, nothing created.</summary>
	public bool Cancel()
	{
		if ( State != LobbyPlayerFlowState.Naming )
			return false;

		ActiveSlot = -1;
		State = LobbyPlayerFlowState.Browsing;
		return true;
	}

	/// <summary>
	/// Returning to the buttons after a choice, for when the controller comes back to the lobby
	/// (for example the island entry was cancelled).
	/// </summary>
	public bool Back()
	{
		if ( State != LobbyPlayerFlowState.Committed )
			return false;

		Result = null;
		ActiveSlot = -1;
		State = LobbyPlayerFlowState.Browsing;
		return true;
	}

	/// <summary>
	/// Records the profile the controller created after <see cref="Confirm"/>, so the slot label updates.
	/// Retail writes the directory in 0x5c3030 and selects the slot in 0x5c34a0; this class does neither.
	/// </summary>
	public bool Adopt( LobbyPlayerProfile profile )
	{
		if ( State != LobbyPlayerFlowState.Committed || Result is not { Kind: LobbyPlayerChoiceKind.New } )
			return false;
		if ( profile.Slot != Result.Slot || !IsValidSlot( profile.Slot ) || profile.Name != Result.Name )
			return false;

		slots[profile.Slot] = profile;
		return true;
	}

	/// <summary>Replaces the slot table and clears pending choices when the save directory changes.</summary>
	public void Reload( IEnumerable<LobbyPlayerProfile?> slots )
	{
		var copy = ValidateSlots( slots );
		Array.Copy( copy, this.slots, SlotCount );
		// A directory refresh invalidates any pending choice from the previous snapshot.
		Reset();
	}

	/// <summary>Drops the dialog and the pending choice without touching the slot table.</summary>
	public void Reset()
	{
		State = LobbyPlayerFlowState.Browsing;
		ActiveSlot = -1;
		Result = null;
	}

	private static LobbyPlayerProfile?[] ValidateSlots( IEnumerable<LobbyPlayerProfile?> slots )
	{
		ArgumentNullException.ThrowIfNull( slots );
		var copy = slots.ToArray();
		if ( copy.Length != SlotCount )
			throw new ArgumentException( $"The lobby has {SlotCount} player slots, not {copy.Length}.", nameof( slots ) );
		for ( int slot = 0; slot < SlotCount; ++slot )
		{
			if ( copy[slot] is { } profile && profile.Slot != slot )
				throw new ArgumentException( $"Player slot {profile.Slot} was supplied at index {slot}.", nameof( slots ) );
		}
		return copy;
	}

	private static bool IsValidSlot( int slot ) => slot >= 0 && slot < SlotCount;

	internal static string Filter( string? candidate )
	{
		if ( string.IsNullOrEmpty( candidate ) )
			return "";

		var kept = new char[candidate.Length];
		int length = 0;
		foreach ( char c in candidate )
		{
			if ( c < ' ' || Array.IndexOf( ForbiddenCharacters, c ) >= 0 )
				continue;
			if ( length == MaxNameLength )
				break;
			kept[length++] = c;
		}
		return new string( kept, 0, length );
	}
}
