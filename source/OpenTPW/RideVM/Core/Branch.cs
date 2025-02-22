namespace OpenTPW;

public struct Branch
{
	/// <summary>
	/// Instruction count (vm offset);
	/// </summary>
	public int InstructionOffset { get; set; }

	/// <summary>
	/// Instruction count + opcode count (compiled value)
	/// </summary>
	public int CompiledOffset { get; set; }

	public Branch( int instructionOffset, int compiledOffset )
	{
		this.InstructionOffset = instructionOffset;
		this.CompiledOffset = compiledOffset;
	}
}
