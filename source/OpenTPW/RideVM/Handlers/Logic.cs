namespace OpenTPW;

partial class OpcodeHandlers
{
	public static class Logic
	{
		[OpcodeHandler( Opcode.BRANCH_NZ, "Branch to another location if the \"zero\" flag hasn't been set." )]
		public static void BranchNotZero( ref RideVM vm, Operand location )
		{
			if ( vm.Flags.HasFlag( RideVM.VMFlags.Zero ) )
				return;

			vm.BranchTo( location.Value );
		}

		[OpcodeHandler( Opcode.BRANCH_Z, "Branch to another location if the \"zero\" flag has been set." )]
		public static void BranchZero( ref RideVM vm, Operand location )
		{
			if ( !vm.Flags.HasFlag( RideVM.VMFlags.Zero ) )
				return;

			vm.BranchTo( location.Value );
		}

		[OpcodeHandler( Opcode.BRANCH, "Branch to another location." )]
		public static void Branch( ref RideVM vm, Operand location )
		{
			vm.BranchTo( location.Value );
		}

		[OpcodeHandler( Opcode.BRANCH_NV, "Branch to another location if the \"sign\" flag has been set." )]
		public static void BranchNegativeValue( ref RideVM vm, Operand location )
		{
			if ( !vm.Flags.HasFlag( RideVM.VMFlags.Sign ) )
				return;

			vm.BranchTo( location.Value );
		}

		[OpcodeHandler( Opcode.BRANCH_PV, "Branch to another location if the \"sign\" flag hasn't been set." )]
		public static void BranchPositiveValue( ref RideVM vm, Operand location )
		{
			if ( vm.Flags.HasFlag( RideVM.VMFlags.Sign ) )
				return;

			vm.BranchTo( location.Value );
		}

		[OpcodeHandler( Opcode.JSR, "Jump to a subroutine." )]
		public static void JumpSubRoutine( ref RideVM vm, Operand location )
		{
			vm.Stack.Enqueue( vm.CurrentPos );
			vm.BranchTo( location.Value );
		}

		[OpcodeHandler( Opcode.RETURN, "Return from a subroutine." )]
		public static void Return( ref RideVM vm )
		{
			int targetPos = vm.Stack.Dequeue();
			vm.CurrentPos = targetPos;
		}
	}
}
