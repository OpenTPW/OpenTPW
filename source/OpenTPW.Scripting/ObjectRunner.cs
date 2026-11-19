namespace OpenTPW;

/// <summary>
/// Runs an object's RSSE script on the retail fixed-step scheduler clock via <see cref="RideVM"/> and drives its
/// <see cref="AnimPlayer"/> (Script_Tick US 0x5516b0 / TPI 0x6a6cf0, Script_Step US 0x551cb0 / TPI 0x6a73a0).
/// </summary>
public sealed class ObjectRunner : IRideVMHost
{
	/// <summary>
	/// Fixed park simulation step (31 ms = 0x1f) shared by both games:
	/// System_UpdateStateMachine (US 0x54e360) state 10 calling Script_Tick at US 0x54f56b,
	/// and FUN_006a4fd0 (TPI 0x6a4fd0) at 0x6a5057 (<c>add eax, 0x1f</c>) calling Script_Tick at 0x6a508c.
	/// </summary>
	public const int DefaultTickMs = 31;

	private readonly RideVM vm;
	private readonly AnimSet anims;
	private readonly AnimPlayer player = new();
	private readonly Func<RideVM, Opcode, uint[], int?>? onCall;
	private int clock;
	private uint tick;
	private uint seed;

	public int TickMs { get; }

	/// <summary>Milliseconds currently reached on the script scheduler clock.</summary>
	public int ClockMs => clock;

	/// <summary>Guest count returned by <see cref="Opcode.BOUNCING"/> (TPI 0x6aa790); 0 keeps rides idle.</summary>
	public int Riders { get; set; }

	public bool IsRunning => !vm.IsStopped;

	public ObjectRunner( byte[] rseBytes, AnimSet anims, uint id, int tickMs = DefaultTickMs, Func<RideVM, Opcode, uint[], int?>? onCall = null )
	{
		ArgumentNullException.ThrowIfNull( rseBytes );
		ArgumentNullException.ThrowIfNull( anims );
		if ( tickMs <= 0 ) throw new ArgumentOutOfRangeException( nameof(tickMs) );
		this.anims = anims;
		TickMs = tickMs;
		this.onCall = onCall;
		seed = 12345u + id * 7919u;
		using var stream = new MemoryStream( rseBytes, writable: false );
		vm = new RideVM( stream, this, (int)id );
	}

	/// <summary>Advances the script to <paramref name="nowMs"/> in <see cref="TickMs"/> scheduler steps.</summary>
	public void Update( int nowMs )
	{
		while ( clock + TickMs <= nowMs && !vm.IsStopped )
		{
			clock += TickMs;
			vm.Tick( tick++ );
		}
		clock = Math.Max( clock, nowMs - TickMs );
	}

	/// <summary>Active animation clip and absolute MD2 frame at <paramref name="nowMs"/>.</summary>
	public (ModelAnimation? Clip, float Frame) Sample( int nowMs ) => player.Sample( nowMs );

	/// <summary>Current animation pose at <paramref name="nowMs"/>, or null when no clip is playing.</summary>
	public ModelPose? Pose( ModelFile model, int nowMs )
	{
		var (clip, frame) = player.Sample( nowMs );
		return clip?.Evaluate( model, frame );
	}

	int IRideVMHost.Now => clock;

	uint IRideVMHost.Random() => seed = unchecked(seed * 1103515245u + 12345u);

	int? IRideVMHost.Call( RideVM vm, Opcode opcode, uint[] operands ) => opcode switch
	{
		Opcode.BOUNCING => Riders,
		_ => onCall?.Invoke( vm, opcode, operands )
	};

	int IRideVMHost.PlayAnimation( RideVM vm, int slot, int variant, bool loop, float speed ) =>
		player.Play( anims.Find( slot, variant ), loop, speed, clock );

	void IRideVMHost.FlushAnimation( RideVM vm ) => player.Flush();
}
