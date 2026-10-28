using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>Screen-space key effects from 0x595d30, 0x4b6a30 and 0x4b6b50.</summary>
internal sealed class LobbyKeyParticles
{
	/// <summary>Screen-space particle state in retail integer units, updated by 0x51cc80/0x51dbc0.</summary>
	internal sealed class Particle
	{
		internal int X, Y, Z, Vx, Vy, Vz, Life, InitialLife;
		internal short Size;
		internal ushort Angle;
		internal uint Colour;
		internal int Sequence, Frame;
		internal bool New = true;
	}
	/// <summary>Running emitter: its raw Tp2.plb record (read at retail offsets), lives, birth countdown and the four phase rates.</summary>
	private sealed class Emitter
	{
		internal byte[] Data = null!;
		internal int Id, X, Y, Z, Life, InitialLife, Countdown, InitialCount, Limit;
		internal int[] Rates = new int[4];
		internal List<Particle> Particles = new();
		internal int Int( int offset ) => BinaryPrimitives.ReadInt32LittleEndian( Data.AsSpan( offset, 4 ) );
		internal short Short( int offset ) => BinaryPrimitives.ReadInt16LittleEndian( Data.AsSpan( offset, 2 ) );
	}
	/// <summary>Point force from the PLB influencer table; when its life ends it starts <c>Child</c> (-1 for none) at its position.</summary>
	private sealed record Influencer( int X, int Y, int Z, int Force, int Child ) { internal int Life; }
	private readonly byte[] library;
	private int density;
	private readonly List<Emitter> emitters = new();
	private readonly List<Influencer> influencers = new();
	private int random = 1, steps;
	private double milliseconds;
	private static readonly int[] sine = Enumerable.Range( 0, 512 )
		// 0x51ba40 samples sin(2*i*0.00613591796875)*256 and truncates to an integer.
		.Select( i => (int)(Math.Sin( i * 2 * 0.00613591796875 ) * 256) ).ToArray();
	internal IEnumerable<Particle> Particles => emitters.SelectMany( emitter => emitter.Particles );

	internal LobbyKeyParticles( byte[] library, int density )
	{
		if ( library.Length != 35704 || BinaryPrimitives.ReadInt32LittleEndian( library ) != 105 ||
			BinaryPrimitives.ReadInt32LittleEndian( library.AsSpan( 4 ) ) != 320 ||
			BinaryPrimitives.ReadInt32LittleEndian( library.AsSpan( 33608 ) ) != 20 ||
			BinaryPrimitives.ReadInt32LittleEndian( library.AsSpan( 33612 ) ) != 104 )
			throw new InvalidDataException( "Unsupported key particle library" );
		if ( density < 1 || density > 4096 ) throw new ArgumentOutOfRangeException( nameof(density) );
		this.library = library; this.density = density;
		// KeyPuff's child is the stationary class-4 repeller (0x51ea80/0x51d630).
		int child = 33616 + 5 * 104;
		if ( BinaryPrimitives.ReadInt32LittleEndian( library.AsSpan( child + 4 ) ) != 4 ||
			BinaryPrimitives.ReadInt32LittleEndian( library.AsSpan( child + 0x40 ) ) != -2 ||
			BinaryPrimitives.ReadInt32LittleEndian( library.AsSpan( child + 0x4c ) ) != 60 )
			throw new InvalidDataException( "Unsupported KeyPuff repeller" );
	}

	private int Next() => random = unchecked(random * 0x343fd + 0x269ec3);
	// 0x51df30 uses SAR/IDIV; 0x51df70 additionally masks to 15 bits.
	private int Signed( int range ) => range == 0 ? 0 : (Next() >> 16) % range;
	private int Positive( int range ) => range == 0 ? 0 : ((Next() >> 16) & 0x7fff) % range;
	private int Scale( int count ) => count == 0 ? 0 : Math.Clamp( density * count >> 10, 1, int.MaxValue );

	internal void Start( int id, int x, int z )
	{
		// 90 is MessageTag1, placed on a UI element whose help is shown (0x484e40).
		if ( id is not (87 or 88 or 90 or 97 or 98) ) throw new ArgumentOutOfRangeException( nameof(id) );
		var emitter = new Emitter { Id = id, Data = library.AsSpan( 8 + id * 320, 320 ).ToArray(), X = x >> 4, Y = 0, Z = z >> 4 };
		bool animated = id is 88 or 90 or 97;
		if ( emitter.Data[4] != (id == 97 ? 0 : id == 90 ? 3 : 4) || emitter.Data[5] != 1 || emitter.Short( 0x0e ) != 0 ||
			emitter.Short( 0x94 ) != (animated ? 12 : 15) || emitter.Short( 0x96 ) != (animated ? 16 : 1) || emitter.Short( 0xa2 ) != 1000 ||
			emitter.Int( 0x58 ) != 0 || emitter.Int( 0x98 ) != -1 )
			throw new InvalidDataException( $"Unsupported key emitter {id}" );
		// Even a spread of -1 consumes three RNG values although all three remainders are zero.
		// Omitting these calls shifts every subsequent lifetime and radial velocity.
		int spread = emitter.Short( 0x12 );
		Signed( spread ); Signed( spread ); Signed( spread );
		emitter.Life = emitter.InitialLife = emitter.Int( 0x20 );
		emitter.InitialCount = Scale( emitter.Int( 0x60 ) );
		ScaleEmitter( emitter );
		emitters.Insert( 0, emitter );
		for ( int i = 0; i < emitter.InitialCount; i++ ) Spawn( emitter );
		if ( emitter.Int( 0xb4 ) >= 0 )
		{
			// KeyPuff starts influencer 5, MessageTag1 influencer 3 (UIRepel: +0x40 = −1, life 30).
			// 0x51da3c compares influencer +0x40 with each emitter's first byte (−1 matches all); it is applied
			// here as a force, as for KeyPuff's −2.
			int child = emitter.Int( 0xb4 );
			if ( child != (id == 90 ? 3 : 5) || emitter.Data[0xba] != 1 ) throw new InvalidDataException( "Unsupported key child effect" );
			AddInfluencer( child, emitter );
		}
	}

	/// <summary>Reapply density to live emitters without respawning particles or resetting their clocks.</summary>
	internal void SetDensity( int value )
	{
		if ( value < 1 || value > 4096 ) throw new ArgumentOutOfRangeException( nameof(value) );
		if ( density == value ) return;
		density = value;
		foreach ( var emitter in emitters ) ScaleEmitter( emitter );
	}
	private void ScaleEmitter( Emitter emitter )
	{
		emitter.Limit = Scale( emitter.Short( 0x64 ) );
		for ( int i = 0; i < 4; i++ )
		{
			// 0x51e480 scales signed emission rates. A negative rate means one birth per -rate ticks.
			int rate = unchecked((sbyte)emitter.Data[0x68 + i]);
			int scaled = density * rate >> 10;
			emitter.Rates[i] = rate == 0 ? 0 : scaled == 0 ? 1 : Math.Min( 127, scaled );
		}
	}

	internal void Stop( int id )
	{
		// 0x51c5d0 writes -2: stop births without firing the -1 end effect or killing live particles.
		foreach ( var emitter in emitters.Where( emitter => emitter.Id == id ) ) emitter.Life = -2;
	}

	private void AddInfluencer( int id, Emitter emitter )
	{
		int offset = 33616 + id * 104;
		int Read( int field ) => BinaryPrimitives.ReadInt32LittleEndian( library.AsSpan( offset + field, 4 ) );
		influencers.Insert( 0, new Influencer( emitter.X, emitter.Y, emitter.Z, Read( 0x40 ),
			BinaryPrimitives.ReadInt16LittleEndian( library.AsSpan( offset + 0x46, 2 ) ) ) { Life = Read( 0x4c ) } );
	}

	private void Spawn( Emitter emitter )
	{
		// 0x54a970 initializes the lobby pool with 0x800 particles, independently of per-emitter limits.
		if ( Particles.Count() >= 2048 ) return;
		int life = emitter.Int( 0x78 );
		life += Signed( life >> 2 );
		var p = new Particle { X = emitter.X, Y = emitter.Y, Z = emitter.Z, Life = life, InitialLife = life == 0 ? 1 : life,
			Size = emitter.Short( 0x74 ), Colour = unchecked((uint)emitter.Int( 0x114 )), Sequence = emitter.Short( 0x94 ) };
		if ( emitter.Data[0x6d] != 0 ) p.Angle = (ushort)((Next() >> 16) & 0x7fff);
		if ( emitter.Int( 0x44 ) != 0 || emitter.Int( 0x48 ) != 0 || emitter.Int( 0x4c ) != 0 )
		{
			int angle = ((Next() >> 16) & 0xfff) >> 3;
			p.X += sine[(angle + 128) & 511] * Positive( emitter.Int( 0x44 ) ) * 4 >> 10;
			p.Y += Signed( emitter.Int( 0x48 ) );
			p.Z -= sine[angle] * Positive( emitter.Int( 0x4c ) ) * 4 >> 10;
		}
		if ( emitter.Int( 0x5c ) != 0 )
		{
			int direction = ((Next() >> 16) & 0xfff) >> 3;
			int speed = Positive( emitter.Int( 0x5c ) );
			p.Vx = sine[(direction + 128) & 511] * speed * 4 >> 10;
			p.Vy = Signed( emitter.Int( 0x5c ) );
			p.Vz = sine[direction] * speed * 4 >> 10;
		}
		else
		{
			// 0x51cc80 skips the angular draw for zero radial speed (KeySparkle).
			p.Vx = emitter.Int( 0x38 ) + Signed( emitter.Short( 0x50 ) );
			p.Vy = emitter.Int( 0x3c ) + Signed( emitter.Short( 0x50 ) );
			p.Vz = emitter.Int( 0x40 ) + Signed( emitter.Short( 0x50 ) );
		}
		emitter.Particles.Insert( 0, p );
	}

	internal void Advance( float seconds )
	{
		if ( !float.IsFinite( seconds ) || seconds < 0 ) throw new ArgumentOutOfRangeException( nameof(seconds) );
		milliseconds += seconds * 1000.0;
		// 0x54a970 advances the particle clock to the first 31ms tick at or beyond now.
		while ( milliseconds > 0 ) { milliseconds -= 31; Tick(); }
	}

	internal void Tick()
	{
		if ( ++steps < 2 ) return;
		foreach ( var emitter in emitters.ToArray() )
		{
			// +0x26 suppresses emitter ageing for KeySparkle; particle life still advances.
			if ( emitter.Short( 0x26 ) == 0 ) emitter.Life--;
			// Key -> attractor -> Key2 is an authored chain, not a second introduction event.
			if ( emitter.Life == -1 && emitter.Short( 0x10 ) >= 0 )
			{
				if ( emitter.Short( 0x10 ) != 4 || emitter.Data[0xbb] != 1 ) throw new InvalidDataException( "Unsupported key end effect" );
				AddInfluencer( 4, emitter );
			}
			if ( emitter.Life < 0 || emitter.Particles.Count >= emitter.Limit ) continue;
			int stage = emitter.InitialLife == 0 ? 3 : (emitter.Life << 2) / emitter.InitialLife;
			int rate = emitter.Rates[stage == 0 ? 3 : stage == 1 ? 2 : stage == 2 ? 1 : 0];
			if ( rate < 0 )
			{
				if ( --emitter.Countdown < 1 ) { Spawn( emitter ); emitter.Countdown = -rate; }
			}
			else for ( int i = 0; i < rate; i++ ) Spawn( emitter );
		}
		foreach ( var influence in influencers.ToArray() )
		{
			// 0x51d630 applies the class-4 repeller before ordinary acceleration and friction.
			if ( --influence.Life < 0 )
			{
				if ( influence.Child >= 0 ) Start( influence.Child, influence.X << 4, influence.Z << 4 );
				continue;
			}
			foreach ( var p in Particles )
			{
				int dx = (influence.X - p.X) / 4, dy = (influence.Y - p.Y) / 4, dz = (influence.Z - p.Z) / 4;
				int distance = ((dx * dx + dy * dy + dz * dz) >> 8) + 10;
				p.Vx += influence.Force * dx / distance; p.Vy += influence.Force * dy / distance; p.Vz += influence.Force * dz / distance;
			}
		}
		influencers.RemoveAll( influence => influence.Life < 0 );
		foreach ( var emitter in emitters )
		{
			foreach ( var p in emitter.Particles )
			{
				if ( --p.Life < 0 ) continue;
				// 0x51dbc0 indexes the sixteen authored colours by remaining lifetime, not elapsed time.
				p.Colour = unchecked((uint)emitter.Int( 0xd8 + (emitter.Int( 0x78 ) < 1 ? 0 : p.Life * 15 / p.InitialLife) * 4 ));
				int frames = emitter.Short( 0x96 );
				p.Frame = frames == 0 ? 0 : frames - 1 - (frames - 1) * p.Life / p.InitialLife;
				p.Size = (short)((emitter.Short( 0x74 ) - emitter.Short( 0xa6 )) * p.Life / p.InitialLife + emitter.Short( 0xa6 ));
				int rotation = emitter.Short( 0xbe );
				if ( rotation != emitter.Short( 0xc6 ) ) rotation += Positive( emitter.Short( 0xc6 ) - rotation );
				p.Angle = unchecked((ushort)(p.Angle + rotation));
				p.Vx += Signed( emitter.Int( 0x84 ) ); p.Vy += Signed( emitter.Int( 0x88 ) ); p.Vz += Signed( emitter.Int( 0x8c ) );
				int friction = emitter.Int( 0x7c );
				p.Vx -= unchecked(friction * p.Vx) / 1024; p.Vy -= unchecked(friction * p.Vy) / 1024; p.Vz -= unchecked(friction * p.Vz) / 1024;
				p.Vy -= emitter.Int( 0x80 );
				// Newly allocated particles receive forces immediately, but move from the following tick.
				if ( p.New ) p.New = false;
				else { p.X += p.Vx; p.Y += p.Vy; p.Z += p.Vz; }
			}
			emitter.Particles.RemoveAll( p => p.Life < 0 );
		}
		emitters.RemoveAll( emitter => emitter.Life < 0 && emitter.Particles.Count == 0 );
	}
}
