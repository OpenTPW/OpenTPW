namespace OpenTPW.UI;

internal sealed class ButtonHoverEffect
{
	// Tp2.plb effect 35 / SPR_PA.ESP group 12 selects these glint frames.
	private const int FirstGlintFrame = 70;
	private const int GlintFrameCount = 16;
	private const float PeakOpacity = .5f;
	private const float BaseSizeScale = .5f;
	private const float GlowSizeScale = .5f;
	private readonly ButtonParticles simulation = new();
	private float previousTime = Time.Now;
	private static Texture[]? stars;
	private static Material? material;

	public void Draw( Rectangle bounds, bool hovered, float scale = 1 )
	{
		float now = Time.Now;
		simulation.Advance( Math.Max( 0, now - previousTime ), hovered );
		previousTime = now;
		if ( simulation.Particles.Count == 0 )
		{
			return;
		}

		if ( stars == null )
		{
			// Tp2.plb effect 35, sprite group 12. SPR_PA.ESP maps that group
			// to the sixteen four-point glint frames beginning at frame 70.
			using var source = FileSystem.OpenRead( "esprites/Generic/Particles/SPR_PA.TPC" );
			stars = TpcSpriteBank.Read( source ).Skip( FirstGlintFrame ).Take( GlintFrameCount )
				.Select( frame => new Texture( frame.Data, frame.Width, frame.Height ) ).ToArray();
			if ( stars.Length != GlintFrameCount )
			{
				throw new InvalidDataException( "Missing Button particle animation frames." );
			}
		}

		material ??= new Material( Path.Combine( AppContext.BaseDirectory, "content", "shaders", "ui_particles.shader" ), MaterialFlags.DisableDepth );
		using var context = new ResourceContext();
		foreach ( var particle in simulation.Particles )
		{
			float lifeProgress = particle.Age / particle.Lifetime;
			int frame = Math.Min( stars.Length - 1, (int)(lifeProgress * stars.Length) );
			context.Set( "Color", stars[frame] );
			// Fade in and out over one half of a sine wave.
			float alpha = MathF.Sin( MathF.PI * particle.Age / particle.Lifetime ) * PeakOpacity;
			float size = particle.Size * scale * (BaseSizeScale + alpha * GlowSizeScale);
			var center = new Vector2( bounds.X + particle.Position.X * bounds.Width, bounds.Y + particle.Position.Y * bounds.Height );
			context.Set( "ParticleColor", new System.Numerics.Vector4( 1, 1, 1, alpha ) );
			Graphics.Quad( new Rectangle( center - new Vector2( size, size ) / 2, new Vector2( size, size ) ), material );
		}
	}
}
