namespace OpenTPW;

/// <summary>Lobby weather requests (0x5db430) and the renderer's bolt queue (0x57b440/0x57c920).</summary>
internal sealed class LobbyWeatherSimulation
{
	internal sealed class Bolt
	{
		internal Vector3 Bottom;
		internal Vector3 Top;
		internal uint Seed;
		internal float Milliseconds = 500;
	}

	private readonly LobbyRandom random;
	private readonly Func<uint> seed;
	internal List<Bolt> Bolts { get; } = new();
	internal bool Flash { get; private set; }
	internal event Action? ThunderRequested;
	internal event Action<int>? AmbientRequested;
	internal bool ConsumeFlash()
	{
		bool requested = Flash;
		Flash = false;
		return requested;
	}

	internal LobbyWeatherSimulation( LobbyRandom random, Func<uint>? seed = null )
	{
		this.random = random;
		// Retail seeds each bolt from the low timestamp-counter bits (0x58d6b0).
		this.seed = seed ?? (() => unchecked((uint)System.Diagnostics.Stopwatch.GetTimestamp()));
	}

	private float Unit() => (random.Next() & 0x3fffffff) * (1f / 1073741824f);

	internal void Update( int lightningMask, Vector3 center, float seconds, bool renderWeather )
	{
		Flash = false;
		if ( lightningMask > 0 && (random.Next() & (uint)lightningMask) == 1 )
		{
			float x = Unit() * 100 + center.X - 50;
			float y = Unit() * 100 + center.Y - 50;
			float bottomX = Unit() * 20 + x - 10;
			float bottomY = Unit() * 20 + y - 10;
			if ( Bolts.Count < 16 )
				Bolts.Add( new Bolt { Bottom = new( bottomX, bottomY, 0 ), Top = new( x, y, 500 ), Seed = seed() | 1 } );
			Flash = renderWeather;
			ThunderRequested?.Invoke();
		}
		// The ambient sound choice consumes this same stream even when sound is unavailable.
		if ( (random.Next() & 15) == 1 )
		{
			int id = (int)(random.Next() % 3) + 3;
			AmbientRequested?.Invoke( id );
		}
		if ( !renderWeather ) return;
		for ( int i = Bolts.Count - 1; i >= 0; i-- )
		{
			Bolts[i].Milliseconds -= seconds * 1000;
			if ( Bolts[i].Milliseconds > 0 ) continue;
			Bolts[i] = Bolts[^1];
			Bolts.RemoveAt( Bolts.Count - 1 );
		}
	}
}
