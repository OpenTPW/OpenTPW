namespace OpenTPW.UI;

/// <summary>Bounded, screen-space sparkles for the original Button particle effect.</summary>
public sealed class ButtonParticles
{
	public const int Capacity = 48;
	// Visual tuning in seconds, pixels, and fractions of the button bounds.
	private const float EmissionsPerSecond = 34;
	private const float MaximumEmissionDelta = .1f;
	private const float BorderProbability = .7f;
	private const float BorderInset = .08f;
	private const float HorizontalDrift = .12f;
	private const float VerticalDrift = .35f;
	private const float MinimumLifetime = .35f;
	private const float LifetimeVariation = .4f;
	private const float MinimumSize = 32;
	private const float SizeVariation = 9;

	public readonly record struct Particle( Vector2 Position, Vector2 Velocity, float Age, float Lifetime, float Size );

	private readonly List<Particle> particles = new();
	private readonly Random random;
	private float emission;

	public IReadOnlyList<Particle> Particles => particles;

	public ButtonParticles( int? seed = null ) => random = seed.HasValue ? new Random( seed.Value ) : new Random();

	public void Advance( float delta, bool hovered )
	{
		if ( !float.IsFinite( delta ) || delta < 0 )
		{
			throw new ArgumentOutOfRangeException( nameof( delta ) );
		}

		for ( int i = particles.Count - 1; i >= 0; i-- )
		{
			var particle = particles[i];
			if ( particle.Age + delta >= particle.Lifetime )
			{
				particles.RemoveAt( i );
			}
			else
			{
				particles[i] = particle with
				{
					Age = particle.Age + delta,
					Position = particle.Position + particle.Velocity * delta
				};
			}
		}

		if ( !hovered )
		{
			emission = 0;
			return;
		}
		// Don't burst after a paused frame or accrue work beyond the fixed budget.

		emission += Math.Min( delta, MaximumEmissionDelta ) * EmissionsPerSecond;
		while ( emission >= 1 && particles.Count < Capacity )
		{
			emission--;
			float x = random.NextSingle(), y = random.NextSingle();
			// Most glints hug the border; some drift across the button face.
			if ( random.NextSingle() < BorderProbability )
			{
				y = random.Next( 2 ) == 0 ? BorderInset : 1 - BorderInset;
			}

			particles.Add( new Particle( new Vector2( x, y ),
				new Vector2( (random.NextSingle() - .5f) * HorizontalDrift, (random.NextSingle() - .5f) * VerticalDrift ),
				0,
				MinimumLifetime + random.NextSingle() * LifetimeVariation,
				MinimumSize + random.NextSingle() * SizeVariation ) );
		}

		if ( particles.Count == Capacity )
		{
			emission = 0;
		}
	}
}
