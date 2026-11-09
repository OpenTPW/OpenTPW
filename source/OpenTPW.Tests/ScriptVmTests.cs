using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>Instruction-level regressions for the shared RSSE VM and its explicit host boundary.</summary>
[TestClass]
public class ScriptVmTests
{
	private const uint V = RideScriptFile.VariableTag, C = RideScriptFile.CodeTag;
	private static uint Op( Opcode opcode ) => RideScriptFile.OpcodeTag | (uint)opcode;

	private static RideVM Create( Host host, uint[] code, int stack = 8, int slice = 50, int id = 0 )
	{
		using var stream = new MemoryStream();
		using ( var writer = new BinaryWriter( stream, Encoding.ASCII, true ) )
		{
			writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
			foreach ( int field in new[] { 0x10f51, 4, stack, slice, 0, 0, 0 } ) writer.Write( field );
			writer.Write( new byte[16] );
			writer.Write( code.Length );
			foreach ( uint word in code ) writer.Write( word );
			writer.Write( 2 );
			writer.Write( new byte[] { (byte)'X', 0 } );
			for ( int i = 0; i < 4; i++ ) { writer.Write( 2 ); writer.Write( new byte[] { (byte)('a' + i), 0 } ); }
		}
		stream.Position = 0;
		return new RideVM( new RideScriptFile( stream ), host, id );
	}

	[TestMethod]
	public void SignedValuesLvAndConditionalBranches()
	{
		var vm = Create( new Host(), [Op( Opcode.COPY ), V, 0xffff, Op( Opcode.BRANCH_NV ), C | 8,
			Op( Opcode.COPY ), V, 99, Op( Opcode.ADD ), V, 2, Op( Opcode.ENDSLICE )] );
		vm.Tick( 0 );
		Assert.AreEqual( 1, vm.Variables[0] );
		Assert.AreEqual( 1, vm.LV );
		Assert.AreEqual( 12, vm.ProgramCounter );
	}

	[TestMethod]
	public void SetlvAndRandomUseRawOperands()
	{
		var vm = Create( new Host(), [Op( Opcode.SETLV ), V | 3, Op( Opcode.RAND ), V, V | 1] );
		vm.Step();
		Assert.AreEqual( unchecked((int)(V | 3)), vm.LV );
		vm.Step();
		Assert.AreEqual( 1, vm.Variables[0] ); // Host returns 14: (14 >> 1) % (raw low16(1) + 1).
	}

	[TestMethod]
	public void ArithmeticAndZeroDivisors()
	{
		var vm = Create( new Host(), [Op( Opcode.MULT ), V, 0xfffd, 7,
			Op( Opcode.DIV ), V | 1, V, 2, Op( Opcode.MOD ), V | 2, V, 4,
			Op( Opcode.DIV ), V | 3, 9, 0] );
		for ( int i = 0; i < 4; i++ ) vm.Step();
		CollectionAssert.AreEqual( new[] { -21, -10, -1, 0 }, vm.Variables );
		Assert.AreEqual( 0, vm.LV );
	}

	[TestMethod]
	public void ReturnAddressesAndDataUseOppositeEndsOfOneStack()
	{
		var vm = Create( new Host(), [Op( Opcode.HUSH ), 42, Op( Opcode.JSR ), C | 5, Op( Opcode.END ),
			Op( Opcode.HOP ), V, Op( Opcode.RETURN )], stack: 2 );
		vm.Tick( 0 );
		Assert.AreEqual( 42, vm.Variables[0] );
		Assert.IsTrue( vm.IsStopped );
	}

	[TestMethod]
	public void EmptyReturnStopsAndZeroStackJsrStillJumps()
	{
		var vm = Create( new Host(), [Op( Opcode.JSR ), C | 3, Op( Opcode.NOP ), Op( Opcode.RETURN )], stack: 0 );
		vm.Step();
		Assert.AreEqual( 3, vm.ProgramCounter );
		vm.Step();
		Assert.IsTrue( vm.IsStopped );
	}

	[TestMethod]
	public void WaitScalesAndAbsoluteWaitDoesNot()
	{
		foreach ( var opcode in new[] { Opcode.WAIT, Opcode.WAITABS } )
		{
			var host = new Host { Clock = 100 };
			var vm = Create( host, [Op( opcode ), 100, Op( Opcode.COPY ), V, 7, Op( Opcode.ENDSLICE )] );
			vm.Speed = 150;
			vm.Tick( 0 );
			host.Clock = 149;
			vm.Tick( 8 );
			Assert.AreEqual( 0, vm.ProgramCounter );
			host.Clock = 150;
			vm.Tick( 16 );
			Assert.AreEqual( opcode == Opcode.WAIT ? 7 : 0, vm.Variables[0] );
			host.Clock = 200;
			if ( opcode == Opcode.WAITABS ) { vm.Tick( 24 ); Assert.AreEqual( 7, vm.Variables[0] ); }
		}
	}

	[TestMethod]
	public void FractionalWaitTruncatesInsteadOfRounding()
	{
		var host = new Host { Clock = 100 };
		var vm = Create( host, [Op( Opcode.WAIT ), 7, Op( Opcode.COPY ), V, 1, Op( Opcode.ENDSLICE )] );
		vm.Speed = 150;
		vm.Tick( 0 );
		host.Clock = 102; vm.Tick( 8 ); Assert.AreEqual( 0, vm.Variables[0] );
		host.Clock = 103; vm.Tick( 16 ); Assert.AreEqual( 1, vm.Variables[0] );
	}

	[TestMethod]
	public void AnimationWaitsAndLoopDeduplication()
	{
		var host = new Host { Clock = 100 };
		var vm = Create( host, [Op( Opcode.TRIGANIM ), 2, 0, V, Op( Opcode.WAIT4ANIM ),
			Op( Opcode.LOOPANIM ), 2, 0, Op( Opcode.LOOPANIM ), 2, 0, Op( Opcode.FLUSHANIM ), Op( Opcode.ENDSLICE )] );
		vm.Tick( 0 );
		Assert.AreEqual( 700, vm.Variables[0] );
		Assert.AreEqual( 800, vm.AnimationDeadline );
		host.Clock = 799; vm.Tick( 8 );
		Assert.AreEqual( 1, host.Animations );
		host.Clock = 800; vm.Tick( 16 );
		Assert.AreEqual( 2, host.Animations );
		Assert.AreEqual( 1, host.Flushes );
	}

	[TestMethod]
	public void WaitanimMinimumDiffersFromTriganimScaling()
	{
		// 400 ms at speed factor 2: (400 - 300) / 2 = 50, raised to the 300 ms minimum after scaling.
		var host = new Host { Clock = 100, Length = 400 };
		var vm = Create( host, [Op( Opcode.WAITANIM ), 1, 0, Op( Opcode.COPY ), V, 9, Op( Opcode.ENDSLICE )] );
		vm.Speed = 150;
		vm.Tick( 0 ); host.Clock = 399; vm.Tick( 8 );
		Assert.AreEqual( 0, vm.Variables[0] );
		host.Clock = 400; vm.Tick( 16 );
		Assert.AreEqual( 9, vm.Variables[0] );
		Assert.AreEqual( 1, host.Animations );
	}

	[TestMethod]
	public void ShortAnimationsFollowRetailUnsignedArithmetic()
	{
		// WAITANIM: length - 300 is loaded unsigned (Script_Step, TPI 0x6a73a0). A 100 ms animation at factor 1 gives
		// 2^32 - 200, so the deadline wraps to clock - 200 and the script goes on at its next visit, without the
		// 300 ms minimum.
		var host = new Host { Clock = 1000, Length = 100 };
		var vm = Create( host, [Op( Opcode.WAITANIM ), 1, 0, Op( Opcode.COPY ), V, 9, Op( Opcode.ENDSLICE )] );
		vm.Tick( 0 );
		Assert.AreEqual( 0, vm.Variables[0] );
		vm.Tick( 8 );
		Assert.AreEqual( 9, vm.Variables[0] );

		// At factor 2 the quotient is about 2^31, so the deadline is far ahead and the script stays waiting.
		host = new Host { Clock = 1000, Length = 100 };
		vm = Create( host, [Op( Opcode.WAITANIM ), 1, 0, Op( Opcode.COPY ), V, 9, Op( Opcode.ENDSLICE )] );
		vm.Speed = 150;
		vm.Tick( 0 );
		host.Clock = 1_000_000; vm.Tick( 8 );
		Assert.AreEqual( 0, vm.Variables[0] );

		// TRIGANIM: 0 (no object) is used as is, giving the 300 ms minimum.
		host = new Host { Clock = 100, Length = 0 };
		vm = Create( host, [Op( Opcode.TRIGANIM ), 2, 0, V, Op( Opcode.ENDSLICE )] );
		vm.Tick( 0 );
		Assert.AreEqual( 300, vm.Variables[0] );
		Assert.AreEqual( 400, vm.AnimationDeadline );
	}

	[TestMethod]
	public void SpeedFactorRoundsOnceLikeX87()
	{
		// Rounded once (x87): bit patterns 0x3f3851eb and 0x3f8a3d71. Rounding the product to float first would give
		// 0x3f3851ec and 0x3f8a3d70, one unit off, which changes truncated waits near whole milliseconds.
		var vm = Create( new Host(), [Op( Opcode.END )] );
		vm.Speed = 22;
		Assert.AreEqual( 0x3f3851ebu, BitConverter.SingleToUInt32Bits( vm.SpeedFactor ) );
		vm.Speed = 58;
		Assert.AreEqual( 0x3f8a3d71u, BitConverter.SingleToUInt32Bits( vm.SpeedFactor ) );
	}

	[TestMethod]
	public void SchedulerStaggersIdsAndTurboUsesRawLowByte()
	{
		var vm = Create( new Host(), [Op( Opcode.TURBO ), V | 1, Op( Opcode.ADD ), V, 1, Op( Opcode.BRANCH ), C | 2], slice: 3, id: 3 );
		vm.Tick( 0 ); Assert.AreEqual( 0, vm.ProgramCounter );
		vm.Tick( 3 ); Assert.AreEqual( 1, vm.Variables[0] );
		vm.Tick( 4 ); Assert.AreEqual( 3, vm.Variables[0] );
	}

	[TestMethod]
	public void CriticalInstructionsDoNotSpendBudgetAndUnlockEndsSlice()
	{
		var vm = Create( new Host(), [Op( Opcode.CRIT_LOCK ), Op( Opcode.ADD ), V, 1,
			Op( Opcode.ADD ), V, 1, Op( Opcode.CRIT_UNLOCK ), Op( Opcode.ADD ), V, 1], slice: 1 );
		vm.Tick( 0 );
		Assert.AreEqual( 2, vm.Variables[0] );
		Assert.AreEqual( 8, vm.ProgramCounter );
		vm.Tick( 8 ); Assert.AreEqual( 3, vm.Variables[0] );
	}

	[TestMethod]
	public void HostGetsRawWordsAndOptionalResultWritesFirstVariable()
	{
		var host = new Host { Result = 37 };
		var vm = Create( host, [Op( Opcode.EVENT ), V, 0xffff, C | 1] );
		vm.Step();
		CollectionAssert.AreEqual( new uint[] { V, 0xffff, C | 1 }, host.Operands );
		Assert.AreEqual( 37, vm.Variables[0] );
	}

	[TestMethod]
	public void InvalidOpcodeAndBranchStopExplicitly()
	{
		foreach ( uint[] code in new[] { new uint[] { V }, new uint[] { 0x8000006a }, new uint[] { Op( Opcode.BRANCH ), 0 } } )
		{
			var vm = Create( new Host(), code ); vm.Step(); Assert.IsTrue( vm.IsStopped );
		}
	}

	[TestMethod]
	public void LoaderRejectsTruncatedAndNegativeBlocks()
	{
		var script = Create( new Host(), [Op( Opcode.NOP )] ).Script;
		Assert.AreEqual( 0x10f51u, script.Checksum );
		Assert.ThrowsExactly<InvalidDataException>( () => new RideScriptFile( new MemoryStream( Encoding.ASCII.GetBytes( "NOPE" ) ) ) );
		using var stream = new MemoryStream();
		using ( var writer = new BinaryWriter( stream, Encoding.ASCII, true ) )
		{
			writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
			writer.Write( 0x10f51 ); writer.Write( -1 );
		}
		stream.Position = 0;
		Assert.ThrowsExactly<InvalidDataException>( () => new RideScriptFile( stream ) );
		Assert.ThrowsExactly<EndOfStreamException>( () => new RideScriptFile( new MemoryStream( Encoding.ASCII.GetBytes( "RSSE" ) ) ) );
	}

	[TestMethod]
	public void DisassemblerSharesMetadataAndRejectsTruncation()
	{
		var vm = Create( new Host(), [Op( Opcode.COPY ), V, 0xffff, Op( Opcode.BRANCH ), C] );
		CollectionAssert.AreEqual( new[] { "0000: COPY a, -1", "0003: BRANCH L0000" }, ScriptDisassembler.Disassemble( vm.Script ).ToArray() );
		var broken = Create( new Host(), [Op( Opcode.COPY ), V] );
		Assert.ThrowsExactly<InvalidDataException>( () => ScriptDisassembler.Disassemble( broken.Script ).ToArray() );
	}

	/// <summary>Observable host services for instruction-level tests, independent of game data.</summary>
	private sealed class Host : IRideVMHost
	{
		public int Clock, Animations, Flushes;
		public int Length = 1000;
		public int? Result;
		public uint[] Operands = [];
		public int Now => Clock;
		public uint Random() => 14;
		public int? Call( RideVM vm, Opcode opcode, uint[] operands ) { Operands = operands; return Result; }
		public int PlayAnimation( RideVM vm, int slot, int variant, bool loop, float speed ) { Animations++; return Length; }
		public void FlushAnimation( RideVM vm ) => Flushes++;
	}
}
