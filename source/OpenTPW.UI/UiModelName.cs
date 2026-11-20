namespace OpenTPW.UI;

/// <summary>
/// UI tables refer to control models by a hash of the model's root node name (opcode 1). The hash is
/// Name_HashWithMultiplier (US 0x6588b6 / TPI 0x84ffb6) with the multiplier 47 that model registration passes
/// (Ui_RegisterModel, TPI 0x851e47): for each byte, sign-extended, value = (value XOR byte) * 47, kept to 32 bits.
/// </summary>
public static class UiModelName
{
	public const uint Multiplier = 47;

	public static uint Hash( ReadOnlySpan<byte> name, uint multiplier = Multiplier )
	{
		uint value = 0;
		foreach ( byte b in name )
		{
			if ( b == 0 ) break; // the retail loop stops at the terminator
			value = unchecked((value ^ (uint)(sbyte)b) * multiplier);
		}
		return value;
	}

	public static uint Hash( string name, uint multiplier = Multiplier ) => Hash( System.Text.Encoding.Latin1.GetBytes( name ), multiplier );
}
