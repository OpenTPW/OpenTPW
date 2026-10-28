namespace OpenTPW;

/// <summary>0x579900 initializes the cube; 0x57c560 scrolls it and 0x57b5e0 wraps each point.</summary>
internal sealed class LobbyRainSimulation
{
	private readonly Vector3[] drops = new Vector3[1024];
	private Vector3 offset;
	internal static readonly Vector3 Direction = InitialDirection();
	private static Vector3 InitialDirection()
	{
		// 0x56f0d3 calls 0x579f30(0, 0.3). Its final writes overwrite the earlier,
		// stronger direction with wind layer 3 scaled by 0.2; keep that small residual.
		float amplitude = 0.3f * 0.0017f;
		double radians = (double)MathF.PI * 0.0055555557f;
		float x = (float)(Math.Sin( (270.0 + 1.8f) * radians ) * amplitude) * 0.2f;
		float z = (float)(Math.Cos( (270.0 + 2.9f) * radians ) * amplitude) * 0.2f;
		float inverseLength = 1 / MathF.Sqrt( z * z + x * x + 1 );
		// Convert native Y-up to engine Z-up once, before both scrolling and streak projection.
		return new( x * inverseLength, z * inverseLength, -inverseLength );
	}
	internal LobbyRainSimulation( Func<int>? next = null )
	{
		next ??= RetailRandom.Next;
		float Coordinate() => ((next() & 0x3fff) - 8192f) * (1f / 2048);
		for ( int i = 0; i < drops.Length; i++ )
		{
			float x = Coordinate(), z = Coordinate(), y = Coordinate();
			drops[i] = new( x, y, z );
		}
	}
	internal void Advance( float seconds )
	{
		// 0x57c560 subtracts horizontal phase, then 0x57b5e0 subtracts it again.
		// The resulting particle translation follows Direction on all three engine axes.
		var moved = offset + Direction * (seconds * 5);
		offset = new( Wrap( moved.X ), Wrap( moved.Y ), Wrap( moved.Z ) );
	}
	internal Vector3 Point( int index, Vector3 camera )
	{
		var point = drops[index];
		return new( Wrap( point.X - CameraOffset( camera.X ) + offset.X ), Wrap( point.Y - CameraOffset( camera.Y ) + offset.Y ),
			Wrap( point.Z + CameraOffset( -camera.Z ) + offset.Z ) );
	}
	internal static float CameraOffset( float coordinate ) => (((int)((coordinate * 0.1f + 4) * 16384)) & 0x1ffff) / 16384f - 4;
	internal static float Wrap( float value ) => value > 4 ? value - 8 : value < -4 ? value + 8 : value;
}
