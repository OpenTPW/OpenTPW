using System.Numerics;

namespace OpenTPW;

/// <summary>
/// The park world's random stream, World_Random (US 0x516330 / TPI 0x6493e0), a method of the global park object
/// (US 0x7cf83c / TPI 0xaa1484, state at +0x1da708 / +0x1eab3c). The RSSE <c>RAND</c> opcode draws from it
/// (US 0x55398a / TPI 0x6a8c82), and so do other park systems: one stream per park, not one per script. The world
/// seeds it with <c>time(NULL)</c> when it is set up (US 0x5156a0 / TPI 0x648070) and saves and loads the state with
/// the park, so <see cref="State"/> is settable.
/// </summary>
public sealed class ParkRandom( uint seed )
{
	public uint State { get; set; } = seed;

	/// <summary>A stream seeded from the clock in seconds, as the world set-up does.</summary>
	public static ParkRandom FromClock() => new( unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()) );

	/// <summary>The next value: the state times 0x19660d plus 0x3c6ef35f, rotated right by 13 and stored, returned as its absolute value.</summary>
	public uint Next()
	{
		uint state = BitOperations.RotateRight( unchecked(State * 0x19660d + 0x3c6ef35f), 13 );
		State = state;
		// The game negates a negative result; 0x80000000 stays as it is, as two's complement negation leaves it.
		int value = (int)state;
		return unchecked((uint)(value < 0 ? -value : value));
	}
}
