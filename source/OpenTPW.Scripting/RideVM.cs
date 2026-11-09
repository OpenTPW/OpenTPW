namespace OpenTPW;

/// <summary>
/// Runs a ride script the way the game does. The scheduler, the single-step function and the
/// handlers follow Script_Tick (TPI 0x6a6cf0) and Script_Step (TPI 0x6a73a0).
/// The shared loader is Script_LoadRse (US 0x5587f0; TPI 0x6aec50). Opcodes that touch other
/// game systems go to <see cref="Host"/>.
/// </summary>
public sealed class RideVM
{
	/// <summary>
	/// The program counter of a script that has stopped.
	/// </summary>
	public const int Stopped = -10000; // Script_Step (TPI 0x6a73a0) invalid-instruction sentinel.

	public RideScriptFile Script { get; }
	public IRideVMHost Host { get; set; }
	public int Id { get; }

	public string ScriptName { get; private set; } = "Unnamed";
	public int[] Variables { get; }

	/// <summary>
	/// Index of the next code word.
	/// </summary>
	public int ProgramCounter { get; private set; }

	/// <summary>
	/// The register that COPY, SUB, TEST, CMP and friends write, and the conditional branches read.
	/// </summary>
	public int LV { get; private set; }

	public bool IsStopped => ProgramCounter < 0;

	/// <summary>
	/// Ride speed setting; waits and animations are scaled by Speed * 0.01 + 0.5.
	/// </summary>
	public short Speed { get; set; } = 50;
	// Script_Step (TPI 0x6a73a0) computes speed * 0.01f + 0.5f on the x87 stack and stores a float. The product of a
	// short and a float is exact in double, so one rounding to float matches; float arithmetic would round twice.
	public float SpeedFactor => (float)(Speed * (double)0.01f + 0.5f);

	/// <summary>
	/// A duration divided by <see cref="SpeedFactor"/>: Script_Step (TPI 0x6a73a0) divides on the x87 stack and
	/// truncates to a 64-bit integer (Runtime_TruncateFloatToInt64, TPI 0x86fab0); callers keep the low 32 bits.
	/// </summary>
	private int Scale( long duration ) => unchecked((int)(long)(duration / (double)SpeedFactor));

	/// <summary>Script_Step (TPI 0x6a73a0) compares the clock with every deadline unsigned (jb).</summary>
	private bool Reached( int deadline ) => unchecked((uint)Host.Now >= (uint)deadline);

	/// <summary>
	/// Run on every scheduler visit instead of every eighth.
	/// </summary>
	public bool Turbo { get; set; }

	/// <summary>
	/// Deadline for WAIT4ANIM; set by TRIGANIM, or by the animation system.
	/// </summary>
	public int AnimationDeadline { get; set; }

	// Script_Tick (TPI 0x6a6cf0) resets the global critical flag before each slice.
	// Keeping it per instance preserves serial slices without leaking state across independent worlds.
	private bool critical;

	// Shared by JSR (return addresses, from the top) and HUSH / HOP (values, from the bottom)
	private readonly uint[] stack;
	private int returnPointer;
	private int dataPointer;

	private int budget;
	private int waitDeadline;
	private int waitAnimationDeadline;
	// Script_Step (TPI 0x6a73a0) uses this sentinel for no looping slot/variant.
	private int loopingAnimation = 0xFFFF;
	private uint tickCounter;

	public RideVM( Stream stream, IRideVMHost host, int id = 0 ) : this( new RideScriptFile( stream ), host, id )
	{
	}

	public RideVM( RideScriptFile script, IRideVMHost host, int id = 0 )
	{
		Script = script;
		Host = host ?? throw new ArgumentNullException( nameof( host ) );
		Id = id;
		Variables = new int[script.VariableCount];
		stack = new uint[script.StackSize];
		returnPointer = script.StackSize - 1;
	}

	/// <summary>
	/// Call once per frame. The game visits every script each frame but runs it only when
	/// turbo is on or on every eighth frame (staggered by id), for up to a time slice of
	/// instructions; waits and ENDSLICE end the slice early.
	/// </summary>
	public void Update() => Tick( tickCounter++ );

	public void Tick( uint counter )
	{
		if ( IsStopped )
			return;

		if ( !Turbo && ((Id ^ counter) & 7) != 0 )
			return;

		critical = false;
		budget = Script.TimeSlice;

		while ( budget > 0 && !IsStopped )
		{
			Step();

			// Instructions inside a critical section are free
			if ( !critical )
				budget--;
		}
	}

	private static bool IsVariable( uint word ) => (word & RideScriptFile.TagMask) == RideScriptFile.VariableTag;
	private static bool IsCode( uint word ) => (word & RideScriptFile.TagMask) == RideScriptFile.CodeTag;
	private static int Payload( uint word ) => (int)(word & ~RideScriptFile.TagMask);

	/// <summary>
	/// An operand's value the way most handlers decode it: a variable's value, otherwise the
	/// low 16 bits as a signed number.
	/// </summary>
	public int Value( uint word )
	{
		if ( IsVariable( word ) )
			return GetVariable( Payload( word ) );

		return (short)(word & 0xFFFF);
	}

	public int GetVariable( int index ) => index >= 0 && index < Variables.Length ? Variables[index] : 0;

	public void SetVariable( int index, int value )
	{
		if ( index >= 0 && index < Variables.Length )
			Variables[index] = value;
	}

	private void Store( uint word, int value )
	{
		if ( IsVariable( word ) )
			SetVariable( Payload( word ), value );
	}

	private uint Fetch()
	{
		if ( ProgramCounter < 0 || ProgramCounter >= Script.Code.Length )
		{
			ProgramCounter = Stopped;
			return 0;
		}

		return Script.Code[ProgramCounter++];
	}

	/// <summary>
	/// Runs one instruction.
	/// </summary>
	public void Step()
	{
		var word = Fetch();
		if ( IsStopped )
			return;

		// Anything but an opcode word stops the script
		if ( (word & RideScriptFile.TagMask) != RideScriptFile.OpcodeTag || Payload( word ) >= RideScriptFile.ArgumentCounts.Length )
		{
			ProgramCounter = Stopped;
			return;
		}

		var opcode = (Opcode)Payload( word );

		switch ( opcode )
		{
			case Opcode.NOP:
				return;

			case Opcode.CRIT_LOCK:
				critical = true;
				return;

			case Opcode.CRIT_UNLOCK:
				critical = false;
				budget = 0;
				return;

			case Opcode.ENDSLICE:
				budget = 0;
				return;

			case Opcode.COPY:
			{
				uint a = Fetch(), b = Fetch();
				if ( !IsVariable( a ) )
					return;

				LV = Value( b );
				Store( a, LV );
				return;
			}

			case Opcode.SETLV:
				// The raw word, not decoded
				LV = (int)Fetch();
				return;

			case Opcode.SUB:
			case Opcode.MULT:
			case Opcode.DIV:
			case Opcode.MOD:
			{
				uint a = Fetch(), b = Fetch(), c = Fetch();
				int x = Value( b ), y = Value( c );

				LV = opcode switch
				{
					Opcode.SUB => x - y,
					Opcode.MULT => x * y,
					Opcode.DIV => y == 0 ? 0 : x / y,
					_ => y == 0 ? 0 : x % y
				};

				Store( a, LV );
				return;
			}

			case Opcode.ADD:
			{
				uint a = Fetch(), b = Fetch();
				if ( !IsVariable( a ) )
					return;

				LV = GetVariable( Payload( a ) ) + Value( b );
				Store( a, LV );
				return;
			}

			case Opcode.GETTIME:
			{
				var a = Fetch();
				LV = Host.Now;
				Store( a, LV );
				return;
			}

			case Opcode.RAND:
			{
				// The limit is the operand's raw low 16 bits, even when it's a variable
				uint a = Fetch(), b = Fetch();
				var r = (int)(Host.Random() >> 1);
				LV = Math.Abs( r % ((short)(b & 0xFFFF) + 1) );
				Store( a, LV );
				return;
			}

			case Opcode.TEST:
			{
				var a = Fetch();
				if ( IsVariable( a ) )
					LV = GetVariable( Payload( a ) );
				return;
			}

			case Opcode.CMP:
			{
				uint a = Fetch(), b = Fetch();
				if ( IsVariable( a ) )
					LV = GetVariable( Payload( a ) ) - Value( b );
				return;
			}

			case Opcode.BRANCH:
			case Opcode.BRANCH_Z:
			case Opcode.BRANCH_NZ:
			case Opcode.BRANCH_NV:
			case Opcode.BRANCH_PV:
			{
				var target = Fetch();
				if ( !IsCode( target ) )
				{
					ProgramCounter = Stopped;
					return;
				}

				bool take = opcode switch
				{
					Opcode.BRANCH => true,
					Opcode.BRANCH_Z => LV == 0,
					Opcode.BRANCH_NZ => LV != 0,
					Opcode.BRANCH_NV => LV < 0,
					_ => LV > 0
				};

				if ( take )
					ProgramCounter = Payload( target );
				return;
			}

			case Opcode.JSR:
			{
				// With a full stack the jump still happens, but RETURN will stop the script
				var target = Fetch();
				if ( !IsCode( target ) )
					return;

				if ( returnPointer >= 0 && returnPointer < stack.Length )
					stack[returnPointer--] = (uint)ProgramCounter | RideScriptFile.CodeTag;

				ProgramCounter = Payload( target );
				return;
			}

			case Opcode.RETURN:
			{
				if ( stack.Length == 0 || returnPointer < -1 || returnPointer >= stack.Length - 1 )
				{
					ProgramCounter = Stopped;
					return;
				}

				var address = stack[++returnPointer];
				if ( IsCode( address ) )
					ProgramCounter = Payload( address );
				return;
			}

			case Opcode.HUSH:
			{
				var a = Fetch();
				if ( dataPointer < 0 || dataPointer >= stack.Length )
					return;

				LV = Value( a );
				stack[dataPointer++] = (uint)LV;
				return;
			}

			case Opcode.HOP:
			{
				var a = Fetch();
				if ( dataPointer <= 0 || dataPointer > stack.Length )
					return;

				LV = (int)stack[--dataPointer];
				Store( a, LV );
				return;
			}

			case Opcode.WAIT:
			case Opcode.WAITABS:
			{
				var n = Fetch();
				if ( waitDeadline != 0 )
				{
					if ( Reached( waitDeadline ) )
					{
						waitDeadline = 0;
						return;
					}
				}
				else
				{
					// WAIT is scaled by the ride speed, WAITABS isn't
					// Script_Step (TPI 0x6a73a0) calls Runtime_TruncateFloatToInt64 (TPI 0x86fab0),
					// which temporarily selects x87 truncation, rather than rounding to nearest.
					waitDeadline = unchecked(Host.Now + (opcode == Opcode.WAIT ? Scale( Value( n ) ) : Value( n )));
				}

				// Run this instruction again on the next visit
				ProgramCounter -= 2;
				budget = 0;
				return;
			}

			case Opcode.WAIT4ANIM:
				if ( AnimationDeadline == 0 )
					return;

				if ( Reached( AnimationDeadline ) )
				{
					AnimationDeadline = 0;
					return;
				}

				ProgramCounter -= 1;
				budget = 0;
				return;

			case Opcode.FLUSHANIM:
				Host.FlushAnimation( this );
				return;

			case Opcode.TRIGANIM:
			{
				// Script_Step (TPI 0x6a73a0) waits for length minus 300 ms (at least 300), so the next
				// animation can be queued before this one ends
				uint a = Fetch(), b = Fetch(), c = Fetch();
				if ( IsStopped )
					return;

				// The length is used as returned: 0 (script without an object) gives the 300 ms minimum, not 700.
				var length = Host.PlayAnimation( this, Value( a ), Value( b ), false, SpeedFactor );
				LV = Math.Max( length - 300, 300 );
				Store( c, LV );
				AnimationDeadline = unchecked(Host.Now + Scale( LV ));
				loopingAnimation = 0xFFFF;
				return;
			}

			case Opcode.WAITANIM:
			{
				uint a = Fetch(), b = Fetch();
				if ( IsStopped )
					return;

				if ( waitAnimationDeadline != 0 )
				{
					if ( Reached( waitAnimationDeadline ) )
					{
						waitAnimationDeadline = 0;
						return;
					}
				}
				else
				{
					// Unlike TRIGANIM, Script_Step (TPI 0x6a73a0) loads length - 300 as an unsigned 64-bit value
					// (fild qword, high half zero), divides, and clamps the quotient unsigned (jae 300). For an animation
					// shorter than 300 ms (or none: length 0) the 32-bit result wraps: at speed factor 1 the deadline
					// lands just before now and the script continues on its next visit (no 300 ms minimum); at other
					// factors it lands about 2^31 ms or more ahead and the script effectively stays blocked. Kept as is.
					var length = Host.PlayAnimation( this, Value( a ), Value( b ), false, SpeedFactor );
					uint scaled = unchecked((uint)Scale( unchecked((uint)(length - 300)) ));
					waitAnimationDeadline = unchecked(Host.Now + (int)Math.Max( scaled, 300u ));
					AnimationDeadline = 0;
					loopingAnimation = 0xFFFF;
				}

				ProgramCounter -= 3;
				budget = 0;
				return;
			}

			case Opcode.LOOPANIM:
			{
				uint a = Fetch(), b = Fetch();
				if ( IsStopped )
					return;

				// Nothing to do when this one is already looping
				int key = (Value( b ) << 16) + Value( a );
				if ( key == loopingAnimation )
					return;

				Host.PlayAnimation( this, Value( a ), Value( b ), true, SpeedFactor );
				AnimationDeadline = 0;
				loopingAnimation = key;
				return;
			}

			case Opcode.TURBO:
				Turbo = (Fetch() & 0xFF) != 0;
				return;

			case Opcode.END:
				ProgramCounter = Stopped;
				return;

			case Opcode.NAME:
			{
				var name = Fetch();
				ScriptName = Script.GetString( Payload( name ) );
				return;
			}

			default:
			{
				var operands = new uint[RideScriptFile.ArgumentCounts[(int)opcode]];
				for ( int i = 0; i < operands.Length; i++ )
					operands[i] = Fetch();

				if ( IsStopped )
					return;

				var result = Host.Call( this, opcode, operands );
				if ( result.HasValue && operands.Length > 0 )
					Store( operands[0], result.Value );
				return;
			}
		}
	}

}
