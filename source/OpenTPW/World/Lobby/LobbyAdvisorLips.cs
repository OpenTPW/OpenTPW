using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>Alternating speech boundaries in microseconds (TP.exe 0x595f44), not mouth indices.</summary>
public sealed class LobbyAdvisorLips
{
	private readonly int[] boundaries;
	private int cursor;
	private int nextMouth;
	public bool Speaking { get; private set; } = true;
	public bool Finished { get; private set; }
	public int Mouth { get; private set; } = 1;

	public LobbyAdvisorLips( byte[] bytes )
	{
		if ( bytes.Length < 4 || bytes.Length % 4 != 0 ) throw new InvalidDataException( "Truncated advisor lip boundaries" );
		boundaries = new int[bytes.Length / 4];
		int previous = 0;
		for ( int i = 0; i < boundaries.Length; i++ )
		{
			int value = BinaryPrimitives.ReadInt32LittleEndian( bytes.AsSpan( i * 4, 4 ) );
			if ( i == boundaries.Length - 1 ? value != -1 : value < previous )
				throw new InvalidDataException( "Invalid advisor lip boundary order or terminator" );
			boundaries[i] = value;
			previous = value;
		}
		if ( boundaries.Length == 1 ) { Finished = true; Speaking = false; }
	}

	public void Update( int speechMilliseconds, Func<int> nextRandom )
	{
		if ( speechMilliseconds < 0 ) { Mouth = 1; return; }
		// Retail consumes at most one boundary per update, using a strict time comparison.
		if ( !Finished && speechMilliseconds > boundaries[cursor] / 1000 )
		{
			Speaking = !Speaking;
			if ( ++cursor == boundaries.Length - 1 ) { Finished = true; Speaking = false; }
		}
		if ( !Speaking || Finished ) Mouth = 1;
		else if ( speechMilliseconds > nextMouth )
		{
			Mouth = nextRandom() % 5 + 1;
			nextMouth = checked(speechMilliseconds + 100);
		}
	}
}
