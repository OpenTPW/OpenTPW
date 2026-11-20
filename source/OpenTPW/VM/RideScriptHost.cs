namespace OpenTPW;

/// <summary>Game clock adapter for the shared RSSE VM; park-system opcodes remain unimplemented.</summary>
internal sealed class RideScriptHost : IRideVMHost
{
	private readonly HashSet<Opcode> reported = [];
	public int Now => (int)(Time.Now * 1000);
	// Every ride draws from the park's one random stream, as RAND does in the game; there is no park object to own it
	// yet, so the stream is shared here, seeded from the clock like the game's world set-up.
	private static readonly ParkRandom park = ParkRandom.FromClock();

	public uint Random() => park.Next();

	public int? Call( RideVM vm, Opcode opcode, uint[] operands )
	{
		// Report each missing service once so placeholder park scripts cannot silently appear complete.
		Report( opcode );
		return null;
	}

	public int PlayAnimation( RideVM vm, int slot, int variant, bool loop, float speed )
	{
		Report( loop ? Opcode.LOOPANIM : Opcode.TRIGANIM );
		// No animation system yet: answer like the game's animation start for a missing animation (1000 ms),
		// not 0, which would mean "no object" and send WAITANIM through its wrapped short-animation arithmetic.
		return 1000;
	}

	public void FlushAnimation( RideVM vm ) => Report( Opcode.FLUSHANIM );

	private void Report( Opcode opcode )
	{
		if ( reported.Add( opcode ) )
			Log.Info( $"Ride script service is not implemented: {opcode}" );
	}
}
