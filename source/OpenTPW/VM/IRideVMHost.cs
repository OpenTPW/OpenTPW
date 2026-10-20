namespace OpenTPW;

/// <summary>
/// What a ride script needs from the rest of the game.
/// </summary>
public interface IRideVMHost
{
	/// <summary>
	/// Game clock in milliseconds.
	/// </summary>
	int Now { get; }

	uint Random();

	/// <summary>
	/// Any opcode the VM doesn't run itself (objects, events, guests, sound, ...). The operands are
	/// the raw code words (see <see cref="RideVM.Value"/>). A returned value is stored in the first
	/// operand when that is a variable.
	/// </summary>
	int? Call( RideVM vm, Opcode opcode, uint[] operands );

	/// <summary>
	/// Starts animation <paramref name="variant"/> of <paramref name="slot"/> (0 to 11: C D I L S M E U W B R O)
	/// on the script's object, at <paramref name="speed"/> times normal speed. Returns its length in
	/// milliseconds, or 0 when there is no such animation (the game then assumes 1000).
	/// </summary>
	int PlayAnimation( RideVM vm, int slot, int variant, bool loop, float speed ) => 0;

	void FlushAnimation( RideVM vm ) { }
}
