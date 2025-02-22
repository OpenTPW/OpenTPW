namespace OpenTPW;

partial class OpcodeHandlers
{
	public static class Math
	{
		[OpcodeHandler( Opcode.ADD, "Add two values together" )]
		public static void Add( ref RideVM vm, Operand dest, Operand value )
		{
			dest.Value = dest.Value + value.Value;
		}

		[OpcodeHandler( Opcode.SUB, "Subtract one value from another" )]
		public static void Sub( ref RideVM vm, Operand valueA, Operand valueB, Operand dest )
		{
			dest.Value = valueA.Value - valueB.Value;
		}

		[OpcodeHandler( Opcode.TEST, "Set flags depending on the value given." )]
		public static void Test( ref RideVM vm, Operand value )
		{
			vm.Flags = RideVM.VMFlags.None;

			if ( value.Value == 0 )
				vm.Flags |= RideVM.VMFlags.Zero;

			if ( value.Value < 0 )
				vm.Flags |= RideVM.VMFlags.Sign;
		}

		[OpcodeHandler( Opcode.CMP, "Compare two values and set any flags according to the result." )]
		public static void Compare( ref RideVM vm, Operand valueA, Operand valueB )
		{
			vm.Flags = RideVM.VMFlags.None;

			if ( valueA.Value == valueB.Value )
				vm.Flags |= RideVM.VMFlags.Zero;

			if ( valueA.Value < valueB.Value )
				vm.Flags |= RideVM.VMFlags.Sign;
		}
	}
}
