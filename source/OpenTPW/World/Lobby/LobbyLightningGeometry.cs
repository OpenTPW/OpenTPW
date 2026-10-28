using N = System.Numerics;

namespace OpenTPW;

/// <summary>Seeded recursive subdivision and side branches from 0x57aac0/0x57ae20.</summary>
internal sealed class LobbyLightningGeometry
{
	internal sealed record Stroke( int Depth, List<Vector3> Points );
	private uint random;
	private N.Vector3 direction;
	private readonly List<Stroke> strokes = new();

	internal IReadOnlyList<Stroke> Generate( Vector3 top, Vector3 bottom, uint seed )
	{
		random = seed;
		strokes.Clear();
		var a = Native( top );
		var b = Native( bottom );
		direction = N.Vector3.Normalize( b - a );
		var points = new List<Vector3> { bottom };
		// Match 0x57ae20's recursive emission order: the low endpoint is submitted first.
		Split( a, b, 0, points );
		strokes.Add( new Stroke( 0, points ) );
		return strokes;
	}

	private static N.Vector3 Native( Vector3 p ) => new( p.X, p.Z, p.Y );
	private static Vector3 Modern( N.Vector3 p ) => new( p.X, p.Z, p.Y );
	private uint Next()
	{
		uint value = unchecked( random * 0x13d573 );
		value = (value << 3) | (value >> 29);
		value = unchecked( value + 0xe0423 );
		return random = (value << 4) | (value >> 28);
	}

	private N.Vector3 Midpoint( N.Vector3 a, N.Vector3 b )
	{
		var middle = (a + b) * 0.5f;
		var delta = a - b;
		float scale = unchecked((int)Next()) % 8191 * (1f / 8192);
		scale = unchecked((int)Next()) % 8191 * (1f / 8192) * scale * scale;
		if ( (Next() & 1) != 0 ) scale = -scale;
		Next();
		int axis = unchecked((int)Next()) % 3;
		var normal = middle;
		float square = middle.LengthSquared();
		if ( axis == 0 )
		{
			if ( delta.X != 0 ) normal.X -= square / delta.X;
			else if ( delta.Y != 0 ) normal.Y -= square / delta.Y;
			else normal.Z -= square / delta.Z;
		}
		else if ( axis == 1 )
		{
			if ( delta.Y != 0 ) normal.Y -= square / delta.Y;
			else if ( delta.Z != 0 ) normal.Z -= square / delta.Z;
			else normal.X -= square / delta.X;
		}
		else
		{
			if ( delta.Z != 0 ) normal.Z -= square / delta.Z;
			else if ( delta.X != 0 ) normal.X -= square / delta.X;
			else normal.Y -= square / delta.Y;
		}
		return middle + normal * (delta.Length() * 0.6f * scale / normal.Length());
	}

	private void Split( N.Vector3 a, N.Vector3 b, int depth, List<Vector3> points )
	{
		var middle = Midpoint( a, b );
		if ( (Next() & 511) < 9 && depth < 4 )
		{
			int childDepth = depth + 1;
			float factor = 1f / (childDepth * 96);
			var forward = N.Vector3.Normalize( b - a );
			float along = ((Next() & 1023) + 512) * factor * 2.5f;
			float bias = ((Next() & 1023) + 512) * factor;
			var offset = bias * direction + along * forward;
			if ( N.Vector3.Dot( direction, offset ) < 0 ) offset = direction * bias * 0.5f + offset * 0.5f;
			var end = a + offset;
			var branch = new List<Vector3> { Modern( a ) };
			Split( end, a, childDepth, branch );
			strokes.Add( new Stroke( childDepth, branch ) );
		}
		float threshold = 6 + ((int)(Next() & 4095) - 2048) * 2 * (1f / 2048);
		if ( threshold <= N.Vector3.DistanceSquared( middle, b ) ) Split( middle, b, depth, points );
		else points.Add( Modern( middle ) );
		if ( threshold <= N.Vector3.DistanceSquared( middle, a ) ) Split( a, middle, depth, points );
		else points.Add( Modern( a ) );
	}
}
