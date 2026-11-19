using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>Full shipped-data regression retained from the ride-script implementation.</summary>
[TestClass]
public class RideScriptTests
{
	/// <summary>Deterministic game services for the original one-minute regression.</summary>
	private sealed class SimulatedHost : IRideVMHost
	{
		public int Clock;
		private uint seed = 12345;

		public int Now => Clock;
		public uint Random() => seed = seed * 1103515245u + 12345u;
		public int? Call( RideVM vm, Opcode opcode, uint[] operands ) => null;
		public void FlushAnimation( RideVM vm ) { }
		public int PlayAnimation( RideVM vm, int slot, int variant, bool loop, float speed ) => 1000;
	}

	[TestMethod]
	[Timeout( 60_000 )]
	public void EveryScriptRunsForAMinute()
	{
		GameData.Require();

		var scripts = GameData.FindFiles( ".rse" );
		Assert.AreEqual( 308, scripts.Count, "The complete TPW corpus is required." );

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

	[TestMethod]
	public void SharedAnimSetPlayerAndRunnerDriveRetailTpwRideInThirtyOneMillisecondTicks()
	{
		GameData.Require();
		const string wadDir = "levels/jungle/rides/bouncy";
		var entries = FileSystem.GetFiles( wadDir )
			.ToDictionary( p => Path.GetFileName( p ), p => p, StringComparer.OrdinalIgnoreCase );
		Assert.IsTrue( entries.TryGetValue( "bouncy.MD2", out var modelPath ) );
		Assert.IsTrue( entries.TryGetValue( "bouncy.RSE", out var scriptPath ) );

		using var modelStream = new MemoryStream( FileSystem.ReadAllBytes( modelPath ), writable: false );
		var model = new ModelFile( modelStream );
		var anims = AnimSet.Load( "bouncy", name => entries.TryGetValue( name, out var path ) ? FileSystem.ReadAllBytes( path ) : null );
		Assert.AreEqual( "CDILSMEUWBRO", anims.SlotLetters );
		Assert.IsFalse( anims.IsEmpty );

		byte[] rse = FileSystem.ReadAllBytes( scriptPath );
		var runner = new ObjectRunner( rse, anims, 1 ) { Riders = 4 };
		Assert.AreEqual( 31, runner.TickMs );
		runner.Update( 92 );
		Assert.AreEqual( 62, runner.ClockMs );
		runner.Update( 93 );
		Assert.AreEqual( 93, runner.ClockMs );

		var activePose = runner.Pose( model, 10_000 );
		Assert.IsNotNull( activePose );

		var player = new AnimPlayer();
		Assert.AreEqual( 1000, player.Play( null, false, 1f, 0 ), "Missing clip must return retail 1000 ms sentinel." );
		var clipA = Enumerable.Range( 0, anims.SlotLetters.Length )
			.Select( slot => anims.Find( slot, 0 ) )
			.First( clip => clip != null && clip.Length > 0 )!;
		int durationMs = player.Play( clipA, loop: false, speed: 1f, nowMs: 0 );
		Assert.IsTrue( durationMs > 0 );
		var secondClip = Enumerable.Range( 0, anims.SlotLetters.Length )
			.Select( slot => anims.Find( slot, 0 ) )
			.FirstOrDefault( clip => clip != null && !ReferenceEquals( clip, clipA ) ) ?? clipA;
		player.Play( secondClip, loop: true, speed: 1f, nowMs: 31 );
		Assert.AreSame( clipA, player.Sample( 31 ).Clip );
		player.Flush();
		Assert.AreEqual( clipA.StartFrame + clipA.Length, player.Sample( durationMs + 500 ).Frame, 0.01f );
	}
}
