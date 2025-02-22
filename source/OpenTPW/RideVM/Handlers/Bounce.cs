namespace OpenTPW;

partial class OpcodeHandlers
{
	public static class Bounce
	{
		[OpcodeHandler( Opcode.BOUNCESETNODE, "Unknown" )]
		public static void BounceSetNode( ref RideVM vm, Operand node )
		{
			// Don't know
		}

		[OpcodeHandler( Opcode.BOUNCESETBASE, "Unknown" )]
		public static void BounceSetBase( ref RideVM vm, Operand @base )
		{
			// Don't know
		}

		[OpcodeHandler( Opcode.BOUNCE, "Let a visitor on for X seconds" )]
		public static void BounceOpcode( ref RideVM vm, Operand visitorId, Operand duration )
		{
			vm.Visitors.Add( visitorId.Value );
		}

		[OpcodeHandler( Opcode.UNBOUNCE, "Let a visitor off" )]
		public static void Unbounce( ref RideVM vm, Operand visitorId )
		{
			vm.Visitors.Remove( visitorId.Value );
		}

		[OpcodeHandler( Opcode.FORCEUNBOUNCE, "FORCIBLY remove a visitor" )]
		public static void ForceUnbounce( ref RideVM vm, Operand visitorId )
		{
			vm.Visitors.Remove( visitorId.Value );
		}

		[OpcodeHandler( Opcode.BOUNCING, "Get the number of visitors currently bouncing on this ride" )]
		public static void Bouncing( ref RideVM vm, Operand dest )
		{
			dest.Value = 0;
		}
	}
}
