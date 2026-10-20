﻿namespace OpenTPW;

/// <summary>
/// Runs a ride script the way the game does. The scheduler, the single-step function and the
/// handlers below follow the game's code (verified in Theme Park Inc's game.exe, whose RSSE VM is
/// the same: same loader, instance layout and opcode table as TP.exe). Opcodes that touch other
/// game systems go to <see cref="Host"/>.
/// </summary>
public sealed class RideVM
{
	/// <summary>
	/// The program counter of a script that has stopped.
	/// </summary>
	public const int Stopped = -10000;

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
	public float SpeedFactor => Speed * 0.01f + 0.5f;

	/// <summary>
	/// Run on every scheduler visit instead of every eighth.
	/// </summary>
	public bool Turbo { get; set; }

	/// <summary>
	/// Deadline for WAIT4ANIM; set by TRIGANIM, or by the animation system.
	/// </summary>
	public int AnimationDeadline { get; set; }

	// The game keeps the critical section flag global
	private static bool critical;

	// Shared by JSR (return addresses, from the top) and HUSH / HOP (values, from the bottom)
	private readonly uint[] stack;
	private int returnPointer;
	private int dataPointer;

	private int budget;
	private int waitDeadline;
	private int waitAnimationDeadline;
	private int loopingAnimation = 0xFFFF;
	private uint tickCounter;

	public RideVM( Stream stream, IRideVMHost host = null, int id = 0 ) : this( new RideScriptFile( stream ), host, id )
	{
	}

	public RideVM( RideScriptFile script, IRideVMHost host = null, int id = 0 )
	{
		Script = script;
		Host = host ?? new DefaultHost();
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
					if ( Host.Now >= waitDeadline )
					{
						waitDeadline = 0;
						return;
					}
				}
				else
				{
					// WAIT is scaled by the ride speed, WAITABS isn't
					waitDeadline = Host.Now + (opcode == Opcode.WAIT ? (int)MathF.Round( Value( n ) / SpeedFactor ) : Value( n ));
				}

				// Run this instruction again on the next visit
				ProgramCounter -= 2;
				budget = 0;
				return;
			}

			case Opcode.WAIT4ANIM:
				if ( AnimationDeadline == 0 )
					return;

				if ( Host.Now >= AnimationDeadline )
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
				// The game waits for the length minus 300 ms (at least 300), so the next
				// animation can be queued before this one ends
				uint a = Fetch(), b = Fetch(), c = Fetch();
				if ( IsStopped )
					return;

				var length = Host.PlayAnimation( this, Value( a ), Value( b ), false, SpeedFactor );
				LV = Math.Max( (length == 0 ? 1000 : length) - 300, 300 );
				Store( c, LV );
				AnimationDeadline = Host.Now + (int)(LV / SpeedFactor);
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
					if ( Host.Now >= waitAnimationDeadline )
					{
						waitAnimationDeadline = 0;
						return;
					}
				}
				else
				{
					var length = Host.PlayAnimation( this, Value( a ), Value( b ), false, SpeedFactor );
					waitAnimationDeadline = Host.Now + Math.Max( (int)(((length == 0 ? 1000 : length) - 300) / SpeedFactor ), 300 );
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

	/// <summary>
	/// Runs the game clock from <see cref="Time.Now"/> and ignores the opcodes the VM doesn't run.
	/// </summary>
	private sealed class DefaultHost : IRideVMHost
	{
		public int Now => (int)(Time.Now * 1000);
		public uint Random() => (uint)System.Random.Shared.Next();
		public int? Call( RideVM vm, Opcode opcode, uint[] operands ) => null;
	}
}
