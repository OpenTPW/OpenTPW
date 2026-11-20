using System.Globalization;

namespace OpenTPW;

/// <summary>Diagnostic RSSE decoding using the same opcode metadata as Script_Step (TPI 0x6a73a0).</summary>
public static class ScriptDisassembler
{
	/// <summary>Formats raw operands without resolving variable values; matches the C++ diagnostic tool.</summary>
	public static string FormatOperand( RideScriptFile script, uint word )
	{
		int payload = (int)(word & ~RideScriptFile.TagMask);
		return (word & RideScriptFile.TagMask) switch
		{
			RideScriptFile.VariableTag => payload < script.VariableNames.Length && !string.IsNullOrEmpty( script.VariableNames[payload] )
				? script.VariableNames[payload] : $"VAR_{payload:D2}",
			RideScriptFile.CodeTag => $"L{payload:x4}",
			RideScriptFile.StringTag => $"\"{script.GetString( payload )}\"",
			0 => payload <= 0xffff ? ((short)word).ToString( CultureInfo.InvariantCulture ) : $"0x{word:x}",
			_ => $"?0x{word:x8}"
		};
	}

	public static IEnumerable<string> Disassemble( RideScriptFile script )
	{
		for ( int pc = 0; pc < script.Code.Length; )
		{
			int start = pc;
			uint word = script.Code[pc++];
			int opcode = (int)(word & ~RideScriptFile.TagMask);
			if ( (word & RideScriptFile.TagMask) != RideScriptFile.OpcodeTag || opcode >= RideScriptFile.ArgumentCounts.Length )
				throw new InvalidDataException( $"Invalid opcode at code word {start}." );
			int count = RideScriptFile.ArgumentCounts[opcode];
			if ( script.Code.Length - pc < count )
				throw new InvalidDataException( $"Truncated instruction at code word {start}." );
			var operands = script.Code.AsSpan( pc, count ).ToArray().Select( x => FormatOperand( script, x ) );
			yield return $"{start:x4}: {(Opcode)opcode} {string.Join( ", ", operands )}".TrimEnd();
			pc += count;
		}
	}
}
