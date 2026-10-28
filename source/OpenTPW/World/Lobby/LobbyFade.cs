namespace OpenTPW;

/// <summary>Camera fade accumulator: 0x5d3dc2..0x5d3e16, once per rendered frame.</summary>
internal sealed class LobbyFade
{
	public int Value { get; private set; }
	public byte Alpha => (byte)Math.Clamp( Value >> 8, 0, 255 );
	public void Update( float target )
	{
		int desired = (int)(Math.Clamp( target, 0, 1 ) * 65536);
		Value += (desired - Value) >> 3;
	}
}
