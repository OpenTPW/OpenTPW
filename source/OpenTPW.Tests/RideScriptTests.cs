using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class RideScriptTests
{
	private sealed class SimulatedHost : IRideVMHost
	{
		public int Clock;
		private uint seed = 12345;

		public int Now => Clock;
		public uint Random() => seed = seed * 1103515245u + 12345u;
		public int? Call( RideVM vm, Opcode opcode, uint[] operands ) => null;
		public int PlayAnimation( RideVM vm, int slot, int variant, bool loop, float speed ) => 1000;
	}

	[TestMethod]
	public void EveryScriptRunsForAMinute()
	{
		GameData.Require();

		var scripts = GameData.FindFiles( ".rse" );
		Assert.IsTrue( scripts.Count > 0 );

		var stopped = scripts.Where( path =>
		{
			var host = new SimulatedHost();
			var vm = new RideVM( FileSystem.OpenRead( path ), host );

			// 30 scheduler visits a second, with every variable starting at 0
			uint tick = 0;
			for ( ; host.Clock <= 60_000 && !vm.IsStopped; host.Clock += 33 )
				vm.Tick( tick++ );

			return vm.IsStopped;
		} ).ToList();

		Console.WriteLine( $"{scripts.Count} scripts" );
		Assert.AreEqual( 0, stopped.Count, string.Join( ", ", stopped ) );
	}
}
