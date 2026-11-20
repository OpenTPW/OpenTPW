namespace OpenTPW;

/// <summary>
/// Game services used by Script_Step (TPI 0x6a73a0); the VM owns no clock or game objects.
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
	/// operand when that is a variable. Null means no write, rather than a zero query result.
	/// </summary>
	int? Call( RideVM vm, Opcode opcode, uint[] operands );

	/// <summary>
	/// Starts animation <paramref name="variant"/> of <paramref name="slot"/> (0 to 11: C D I L S M E U W B R O)
	/// on the script's object, at <paramref name="speed"/> times normal speed. Returns its length in
	/// milliseconds. As in the game, a missing animation returns 1000 (the animation start function); 0 means the
	/// script has no object at all, which the VM treats as retail does (300 ms for TRIGANIM; for WAITANIM the
	/// wrapped arithmetic in <see cref="RideVM"/>).
	/// </summary>
	int PlayAnimation( RideVM vm, int slot, int variant, bool loop, float speed );

	void FlushAnimation( RideVM vm );
}
