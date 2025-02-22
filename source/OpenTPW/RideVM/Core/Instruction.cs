namespace OpenTPW;

public struct Instruction
{
	public readonly Operand[] operands;
	public readonly Opcode opcode;
	public readonly long offset;

	private RideVM vmInstance;

	public Instruction( RideVM vmInstance, long offset, Opcode opcode, Operand[] operands )
	{
		this.offset = offset;
		this.operands = operands;
		this.opcode = opcode;

		this.vmInstance = vmInstance;
	}

	public int GetCount()
	{
		return 1 + operands.Length;
	}

	public void Invoke()
	{
		vmInstance.CallOpcodeHandler( opcode, operands );
	}
}
