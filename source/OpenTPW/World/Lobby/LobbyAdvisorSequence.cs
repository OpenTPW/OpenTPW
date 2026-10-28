namespace OpenTPW;

/// <summary>Default speech gestures built by TP.exe 0x594fd0; indices are zero based.</summary>
public sealed record LobbyAdvisorSequence( int[] Segments, int DurationMilliseconds )
{
	public static LobbyAdvisorSequence Create( int speechMilliseconds, IReadOnlyList<int> durations, Func<int> nextRandom )
	{
		if ( speechMilliseconds < 0 || durations.Count != 15 || durations.Any( duration => duration <= 0 ) )
			throw new ArgumentException( "Advisor speech requires fifteen positive animation durations" );
		int remaining = checked(speechMilliseconds + 500), total = 0;
		var segments = new List<int>();
		for ( int i = 0; ; i++ )
		{
			if ( remaining < 1 || i > 18 )
			{
				segments.Add( 14 );
				return new( segments.ToArray(), checked(total + durations[14]) );
			}
			// Retail consumes this draw even for the fixed M14 introduction.
			int segment = nextRandom() % 10;
			if ( i == 0 ) segment = 13;
			else if ( remaining < durations[segment] )
			{
				segment = 9;
				for ( int candidate = 0; candidate < 10; candidate++ )
					if ( remaining <= durations[candidate] ) { segment = candidate; break; }
			}
			segments.Add( segment );
			total = checked(total + durations[segment]);
			remaining -= durations[segment];
		}
	}
}
