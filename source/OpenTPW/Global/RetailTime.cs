namespace OpenTPW;

/// <summary>Time conversions for the retail lobby, in its original numeric units.</summary>
public static class RetailTime
{
	// FUN_005d0c50: CMP EAX, 0x1f4; FMUL [0x6fe8dc] (0.01f).
	public const int MaxFrameMilliseconds = 500;
	public const float TicksPerMillisecond = 0.01f;
	public const float TicksPerSecond = 1000 * TicksPerMillisecond;
	public const float MaxFrameTicks = MaxFrameMilliseconds * TicksPerMillisecond;
	// FUN_0054a970 state 10 advances DAT_0089136c by 0x1f milliseconds.
	public const int ParkStepMilliseconds = 31;

	/// <summary>min(seconds * 1000, 500) * 0.01: 16 ms is 0.16, one unit is 100 ms.</summary>
	public static float TicksFromSeconds( float seconds )
	{
		if ( !float.IsFinite( seconds ) ) throw new ArgumentOutOfRangeException( nameof( seconds ) );
		// Negative input is an OpenTPW clock guard; retail uses an unsigned millisecond delta.
		return Math.Clamp( seconds * 1000, 0, MaxFrameMilliseconds ) * TicksPerMillisecond;
	}

	public static float SecondsFromTicks( float ticks )
	{
		if ( !float.IsFinite( ticks ) ) throw new ArgumentOutOfRangeException( nameof( ticks ) );
		return ticks / TicksPerSecond;
	}

	public static float FrameTicks => TicksFromSeconds( Time.Delta );
	/// <summary>The same capped interval in seconds, for effects authored in seconds.</summary>
	public static float FrameSeconds => SecondsFromTicks( FrameTicks );
}
