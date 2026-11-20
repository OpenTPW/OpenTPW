using OpenTPW.UI;
using System.Globalization;

namespace OpenTPW;

/// <summary>Draws <see cref="LobbyKeyParticles"/> with the SPR_PA sprite atlas. Density comes from the quality preset's PARTICLEDENSITY.</summary>
internal sealed class LobbyKeyParticlePanel : Panel
{
	private readonly LobbyKeyParticles state;
	private readonly LobbyQuality quality;
	private readonly Vertex[] vertices = new Vertex[2048 * 6];
	private readonly Model model;
	private readonly SpriteSequenceFile sequences;
	private readonly SpriteTextureFile sprites;
	private readonly int tileWidth, tileHeight, atlasWidth, atlasHeight;
	private bool affordable;
	private Rectangle? hoveredButton;
	private double hoverStarted;
	private int hoverEmitterCount;
	internal bool ButtonEffectActive => hoveredButton != null;
	internal LobbyKeyParticlePanel()
	{
		quality = LobbyQuality.Load();
		state = new LobbyKeyParticles( FileSystem.ReadAllBytes( "Particle/Tp2.plb" ), quality.ParticleDensity );
		sequences = new SpriteSequenceFile( FileSystem.ReadAllBytes( "esprites/Generic/Particles/SPR_PA.ESP" ) );
		sprites = new SpriteTextureFile( FileSystem.ReadAllBytes( "esprites/Generic/Particles/SPR_PA.TPC" ) );
		// Keep all authored frames: Key2 uses the sixteen-frame sequence 12 after the attraction phase.
		// Transparent gutters prevent one sprite from sampling its atlas neighbour.
		tileWidth = sprites.Frames.Max( frame => frame.Width ) + 2;
		tileHeight = sprites.Frames.Max( frame => frame.Height ) + 2;
		atlasWidth = tileWidth * 16; atlasHeight = tileHeight * ((sprites.Frames.Count + 15) / 16);
		var rgba = new byte[atlasWidth * atlasHeight * 4];
		for ( int index = 0; index < sprites.Frames.Count; index++ )
		{
			var frame = sprites.Frames[index];
			for ( int y = 0; y < frame.Height; y++ )
			for ( int x = 0; x < frame.Width; x++ )
			{
				uint colour = sprites.Palette[frame.Indices[y * frame.Width + x]];
				int offset = (((index / 16) * tileHeight + 1 + y) * atlasWidth + (index % 16) * tileWidth + 1 + x) * 4;
				rgba[offset] = (byte)(colour >> 16); rgba[offset + 1] = (byte)(colour >> 8);
				rgba[offset + 2] = (byte)colour; rgba[offset + 3] = (byte)(colour >> 24);
			}
		}
		var material = new Material( "content/shaders/lobby-particles.shader", MaterialFlags.DisableDepth | MaterialFlags.DisableCulling | MaterialFlags.Additive );
		material.Set( "Color", new Texture( rgba, atlasWidth, atlasHeight, TextureFlags.PointFilter ), SamplerType.PointMinLinearMag );
		model = new Model( vertices, material );
	}
	internal void IntroductionKey() => state.Start( 87, 90000, 7000 );
	/// <summary>Starts and stops the authored Button effect when the pointer enters or leaves a button.</summary>
	internal void HoverButton( Rectangle? bounds )
	{
		// Ui_HandleGlobalMessage (US 0x485780) suppresses the effect when popup help is disabled.
		if ( Level.Current?.Audio is { TooltipsEnabled: false } ) bounds = null;
		if ( hoveredButton == bounds ) return;
		state.Stop( 35 );
		hoveredButton = bounds;
		if ( bounds is not { } b ) return;
		// ButtonSpark_Start (US 0x5ed920) selects 2..5 effect-35 emitters from normalized button area.
		float area = b.Width / 2048f * (b.Height / 1536f);
		hoverEmitterCount = Math.Clamp( 2 - (int)((area - 0.0016f) / 0.0051f * -3), 2, 5 );
		hoverStarted = Time.Now;
		for ( int i = 0; i < hoverEmitterCount; i++ ) state.Start( 35, 0, 0 );
	}
	// Ui_Function_TW_4b6a30 (US address pending) uses the integer centre of element 0x1e0ea (554,170)-(990,437).
	internal void EnterIsland() => state.Start( 98, 50000, (170 + (437 - 170) / 2) * 75000 / 1536 );
	internal void SetAffordable( bool value )
	{
		if ( affordable == value ) return;
		affordable = value;
		// FUN_004b90f0 (US 0x4b90f0)/IslandPage_Update (US 0x4b9340) retain one sparkle emitter while the selected island is affordable.
		if ( value ) state.Start( 97, 50000, (170 + (437 - 170) / 2) * 75000 / 1536 );
		else state.Stop( 97 );
	}
	// The particle clock uses the frontend's elapsed milliseconds, independently of capped camera dt.
	internal void Advance( float seconds )
	{
		state.SetDensity( quality.ParticleDensity );
		if ( hoveredButton is { } bounds )
		{
			// ButtonSpark_Update (US 0x5ed770) expands the ellipse over 400 ms and rotates at 0.4 turns per interval.
			float t = (float)((Time.Now - hoverStarted) / 0.4);
			float radius = (1 + 2 * Math.Clamp( t, 0, 1 )) / 3;
			float cx = (bounds.X + bounds.Width / 2 - 1024) / 1024;
			float cy = (bounds.Y + bounds.Height / 2 - 768) / 768;
			for ( int i = 0; i < hoverEmitterCount; i++ )
			{
				float phase = i / (float)hoverEmitterCount + t * 0.4f;
				int angle = (int)((phase - MathF.Floor( phase )) * 512) & 511;
				float sin = (int)(Math.Sin( angle * Math.Tau / 512 ) * 256) / 256f;
				float cos = (int)(Math.Sin( ((angle + 128) & 511) * Math.Tau / 512 ) * 256) / 256f;
				int x = (int)((cx + bounds.Width / 2048 * radius * sin + 1) * 50000);
				int z = (int)((cy - bounds.Height / 1536 * radius * cos + 1) * 37500);
				state.MoveButtonEmitter( i, x, z );
			}
		}
		state.Advance( seconds );
	}
	/// <summary>
	/// Screen position in retail particle units. Particle_DrawScreenEffect (US address pending) uses integer divisors
	/// 49 and 37, followed by Render_DrawSpriteQuads (US 0x57c620) scaling by 1/1024. These differ from UI design-unit
	/// divisors; retaining the reviewed LobbyFollowup mapping keeps the effect on the button outline.
	/// </summary>
	internal static (float X, float Y) ScreenPosition( int x, int z ) => (((x - 3125) * 16 / 49) / 1024f, ((z * 16 - 37500) / 37) / 1024f);
	protected override void OnRender()
	{
		Array.Clear( vertices );
		int offset = 0;
		foreach ( var particle in state.Particles )
		{
			int index = sequences.FrameIndex( particle.Sequence, particle.Frame, 0, sprites.Frames.Count );
			var frame = sprites.Frames[index];
			float aspect = (float)frame.Width / frame.Height;
			float left = ((index % 16) * tileWidth + 1f) / atlasWidth, top = ((index / 16) * tileHeight + 1f) / atlasHeight;
			float width = (float)frame.Width / atlasWidth, height = (float)frame.Height / atlasHeight;
			var (x, y) = ScreenPosition( particle.X, particle.Z );
			float halfHeight = (particle.Size / 64) / 2048f;
			float angle = (particle.Angle >> 8) * (MathF.Tau / 256);
			float c = MathF.Cos( angle ), s = MathF.Sin( angle );
			// The UI shader flips V once. Convert top-down atlas rows here while preserving
			// the retail clockwise screen rotation and ARGB particle colour.
			Vertex V( float dx, float dy, float u, float v ) => new(
				new Vector3( x + dx * c + dy * s, -(y + dy * c - dx * s), 0 ),
				new Vector3( ((particle.Colour >> 16) & 255) / 255f, ((particle.Colour >> 8) & 255) / 255f, (particle.Colour & 255) / 255f ),
				new Vector2( left + u * width, 1 - top - (1 - v) * height ) ) { MatFlags = particle.Colour >> 24 };
			float halfWidth = halfHeight * aspect;
			var a = V( -halfWidth, -halfHeight, 0, 1 ); var b = V( halfWidth, -halfHeight, 1, 1 );
			var d = V( -halfWidth, halfHeight, 0, 0 ); var e = V( halfWidth, halfHeight, 1, 0 );
			vertices[offset++] = a; vertices[offset++] = b; vertices[offset++] = d;
			vertices[offset++] = b; vertices[offset++] = e; vertices[offset++] = d;
		}
		if ( offset == 0 ) return;
		model.UpdateVertices( vertices ); model.Draw();
	}
}
