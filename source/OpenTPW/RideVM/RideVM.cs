using System.Reflection;

namespace OpenTPW;

public partial class RideVM
{
	public string ScriptName { get; set; } = "Unnamed";

	public bool IsRunning { get; set; }
	public int CurrentPos { get; set; }
	// public string Disassembly => rsseqFile.Disassembly;
	public List<Instruction> Instructions { get; } = new List<Instruction>();

	/// <summary>
	/// Key: String offset
	/// Value: String value
	/// </summary>
	public Dictionary<long, string> Strings { get; } = new Dictionary<long, string>();
	public List<int> Variables { get; set; } = new List<int>();
	public List<string> VariableNames { get; set; } = new List<string>();
	public VMFlags Flags { get; set; } = VMFlags.None;
	public VMConfig Config { get; set; } = new VMConfig();
	public List<Branch> Branches { get; set; } = new List<Branch>();
	public byte[] FileData { get; set; }

	public List<int> Visitors { get; set; } = new();
	public Queue<int> Stack { get; set; } = new();

	private Dictionary<Opcode, MethodInfo> OpcodeHandlers { get; } = Assembly.GetExecutingAssembly().GetTypes()
		.SelectMany( t => t.GetMethods() )
		.Where( x => x.GetCustomAttribute<OpcodeHandlerAttribute>() != null )
		.Select( x => (x.GetCustomAttribute<OpcodeHandlerAttribute>().Opcode, x) )
		.ToDictionary( x => x.Opcode, x => x.x );

	public RideVM( Stream stream )
	{
		// rsseqFile = new RideScriptFile( this, stream );

		// DEBUG: Log implemented opcode counts
		var implementedOpcodes = OpcodeHandlers.Keys.ToList();
		var implementedCount = implementedOpcodes.Count;
		var totalCount = 210; // Total number, see https://opentpw.gu3.me/formats/rsse-vm-instructions.html
		var totalPercent = (float)implementedCount / totalCount * 100;

		Log.Info( $"Implemented {implementedCount} / {totalCount} ({totalPercent.CeilToInt()}%) opcodes" );

		// Set up basic ride variables
		Variables[(int)RideVariables.VAR_RIDECLOSED] = 1;
		Variables[(int)RideVariables.VAR_CAPACITY] = 16;
		Variables[(int)RideVariables.VAR_DURATION] = 30;
	}

	public void Step()
	{
		var instruction = Instructions[CurrentPos++];
		Log.Trace( $"Invoking {instruction.opcode} at position {CurrentPos}" );

		instruction.Invoke();
	}

	public MethodInfo? FindOpcodeHandler( Opcode opcodeId )
	{
		if ( OpcodeHandlers.TryGetValue( opcodeId, out var handlerAttribute ) )
			return handlerAttribute;

		return null;
	}

	public void CallOpcodeHandler( Opcode opcodeId, params Operand[] operands )
	{
		var handlerAttribute = FindOpcodeHandler( opcodeId );

		if ( handlerAttribute == null )
		{
			Log.Warning( $"No handler for {opcodeId}, treating as no-op" );
			return;
		}

		var parameters = new object[] { this };
		parameters = Enumerable.Concat( parameters, operands ).ToArray();

		handlerAttribute?.Invoke( null, parameters );
	}

	public void BranchTo( int value )
	{
		//
		// HACK: Values are provided as offsets in terms of instruction size, i.e.
		// an offset of 7 might be 3 opcodes and 4 operands.
		// To get around this, we convert to a position in the file, and then locate
		// that offset in the instruction list.
		// This is super hacky (and probably slow) and could probably be avoided
		// if the disassembler / file handler gets re-written.
		//

		var fileOffset = value * 4; // Each opcode + operands is 4 bytes
		fileOffset += (int)Instructions.First().offset;

		CurrentPos = Instructions.FindIndex( x => x.offset == fileOffset );
		CurrentPos += 1; // Ignore NO-OP

		Log.Trace( $"Branching to .label_{value} / {fileOffset} (location: {CurrentPos})" );
	}

	private TimeSince TimeSinceLastTick;
	public void Update()
	{
		if ( IsRunning && TimeSinceLastTick > 1f / 5f )
		{
			Step();
			TimeSinceLastTick = 0;
		}
	}

	public void Run()
	{
		IsRunning = !IsRunning;
	}
}
