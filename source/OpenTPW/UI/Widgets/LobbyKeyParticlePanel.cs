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
		material.Set( "Color", new Texture( rgba, atlasWidth, atlasHeight, TextureFlags.PointFilter ), SamplerType.Point );
		model = new Model( vertices, material );
	}
	internal void IntroductionKey() => state.Start( 87, 90000, 7000 );
	/// <summary>MessageTag1 at the integer centre of a UI rectangle (0x484e40: x × 100000 / 2048, y × 75000 / 1536).</summary>
	internal void MessageTag( Rectangle units )
	{
		int left = (int)units.X, top = (int)units.Y, right = (int)(units.X + units.Width), bottom = (int)(units.Y + units.Height);
		state.Start( 90, (left + (right - left) / 2) * 100000 / 2048, (top + (bottom - top) / 2) * 75000 / 1536 );
	}
	// 0x4b6a30 uses the integer centre of element 0x1e0ea (554,170)-(990,437).
	internal void EnterIsland() => state.Start( 98, 50000, (170 + (437 - 170) / 2) * 75000 / 1536 );
	internal void SetAffordable( bool value )
	{
		if ( affordable == value ) return;
		affordable = value;
		// 0x4b6b50/0x4b6da0 retain one sparkle emitter while the selected island is affordable.
		if ( value ) state.Start( 97, 50000, (170 + (437 - 170) / 2) * 75000 / 1536 );
		else state.Stop( 97 );
	}
	// The particle clock uses the frontend's elapsed milliseconds, independently of capped camera dt.
	internal void Advance( float seconds ) { state.SetDensity( quality.ParticleDensity ); state.Advance( seconds ); }
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
			// 0x51b650's integer screen coordinates and size, consumed by 0x578c10.
			float x = ((particle.X - 3125) * 16 / 50) / 1024f;
			float y = ((particle.Z * 16 - 37500) * 2 / 75) / 1024f;
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
