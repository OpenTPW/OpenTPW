namespace OpenTPW.UI;

/// <summary>
/// The area of an element that takes the pointer. Every element starts with its own rectangle; table opcode 4
/// replaces it (Ui_InterpretTable, US 0x65fd58 / TPI 0x84deea). The four kinds and their tests are the retail
/// shape classes; both games' tests are identical.
/// </summary>
public abstract record UiHitShape
{
	public abstract bool Contains( int x, int y );
}

/// <summary>Opcode 4, kind 1 (and every element's default): half-open rectangle (US 0x659e83 / TPI 0x850fe3).</summary>
public sealed record UiRectShape( UiRect Rect ) : UiHitShape
{
	public override bool Contains( int x, int y ) => Rect.Contains( x, y );
}

/// <summary>Opcode 4, kind 2: circle, inside when dx² + dy² &lt; r² (US 0x66c0c4 / TPI 0x85e8f4).</summary>
public sealed record UiCircleShape( short X, short Y, short Radius ) : UiHitShape
{
	public override bool Contains( int x, int y )
	{
		int dx = x - X, dy = y - Y;
		return dx * dx + dy * dy < Radius * Radius;
	}
}

/// <summary>
/// Opcode 4, kind 3: ellipse with centre and radii, tested by scaling x to the vertical radius with integer division
/// (US 0x66c22c / TPI 0x85ea5c).
/// </summary>
public sealed record UiEllipseShape( short X, short Y, short RadiusX, short RadiusY ) : UiHitShape
{
	public override bool Contains( int x, int y )
	{
		if ( RadiusX == 0 ) return false; // the retail division would fault; no table uses a zero radius
		int dx = (x - X) * RadiusY / RadiusX, dy = y - Y;
		return dx * dx + dy * dy < RadiusY * RadiusY;
	}
}

/// <summary>
/// Opcode 4, kind 4: polygon, crossing-number test written as the retail one, including its integer comparison
/// (US 0x66c5a4 / TPI 0x85edea), so points on edges classify the same way.
/// </summary>
public sealed record UiPolygonShape( IReadOnlyList<(short X, short Y)> Points ) : UiHitShape
{
	public override bool Contains( int x, int y )
	{
		if ( Points.Count == 0 ) return false;
		bool inside = false;
		var previous = Points[^1];
		bool previousBelow = y <= previous.Y;
		foreach ( var current in Points )
		{
			bool currentBelow = y <= current.Y;
			if ( previousBelow != currentBelow
				&& ((current.X - x) * (previous.Y - current.Y) <= (current.Y - y) * (previous.X - current.X)) == currentBelow )
				inside = !inside;
			previous = current;
			previousBelow = currentBelow;
		}
		return inside;
	}
}
