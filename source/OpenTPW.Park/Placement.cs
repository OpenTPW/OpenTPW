using System.Numerics;
using Heightfield = OpenTPW.ModelFile.HeightfieldData;

namespace OpenTPW.Park;

// Inside the namespace these aliases win over OpenTPW.Vector2/Vector3 from OpenTPW.Common.
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// How MeshInstance_Create (US 0x463060 / TPI 0x4c3710) is asked to place an instance. The values are the callers'
/// kind words; only the bits placement reads are named.
/// </summary>
[Flags]
public enum PlacementKind
{
	/// <summary>Ground height from the footprint corners (MeshInstance_GroundHeight); without it, the origin corner's height.</summary>
	FootprintHeight = 0x08,
	/// <summary>Every footprint cell counts for the height, whatever the shape says.</summary>
	AllCells = 0x10,
	/// <summary>Turn and translate the root node (MeshInstance_SetTransform).</summary>
	Transform = 0x20,

	/// <summary>Park objects (ParkObject_CreateMesh, US 0x4dd500 / TPI 0x5df740).</summary>
	Object = 0x32f,
	/// <summary>Fixed items, placed at their engine map offset (same caller).</summary>
	Fixed = 0x361,
	/// <summary>Queue pieces (Queue_PlacePiece, US 0x5229e0 / TPI 0x65cea0).</summary>
	QueuePiece = 0x33a,
}

/// <summary>
/// Where an instance ended up: the model origin cell after the corner shift, the occupied rectangle, the ground height and
/// the root node's new rotation and translation.
/// </summary>
/// <param name="OriginX">Cell whose low corner is the model origin.</param>
/// <param name="TopLeftX">Lowest x cell of the rotated footprint rectangle.</param>
/// <param name="ExtentX">Rectangle size along world x (footprint depth at 90/270 degrees).</param>
public sealed record PlacedInstance( int OriginX, int OriginZ, int TopLeftX, int TopLeftZ, int ExtentX, int ExtentZ,
	float Height, int AngleDegrees, Vector3 Translation, bool Transforms )
{
	/// <summary>
	/// The root node's local transform after placement (row vectors). MeshInstance_SetTransform (US 0x467030 / TPI 0x4c9ed0)
	/// right-multiplies the root's 3×3 by a turn about Y (FUN_0046f650, US 0x46f650 / TPI 0x4da8f0: per row, x' = x·c + z·s,
	/// z' = z·c − x·s) and overwrites the root's translation row with <see cref="Translation"/>; the authored root
	/// translation is dropped.
	/// </summary>
	public Matrix4x4 RootLocal( Matrix4x4 authoredRoot )
	{
		if ( !Transforms ) return authoredRoot;
		var (c, s) = Placement.CosSin( AngleDegrees );
		var turn = new Matrix4x4( c, 0, -s, 0, 0, 1, 0, 0, s, 0, c, 0, 0, 0, 0, 1 );
		var turned = authoredRoot with { M41 = 0, M42 = 0, M43 = 0 } * turn;
		return turned with { M41 = Translation.X, M42 = Translation.Y, M43 = Translation.Z, M44 = authoredRoot.M44 };
	}

	/// <summary>Every cell of the occupied rectangle.</summary>
	public IEnumerable<(int X, int Z)> Rectangle()
	{
		for ( int z = TopLeftZ; z < TopLeftZ + ExtentZ; z++ )
			for ( int x = TopLeftX; x < TopLeftX + ExtentX; x++ )
				yield return (x, z);
	}
}

/// <summary>The rules both games use to put a mesh instance on the terrain grid (see <see cref="Place"/>).</summary>
public static class Placement
{
	/// <summary>
	/// Places an instance whose model origin is at cell (<paramref name="cellX"/>, <paramref name="cellZ"/>), turned by
	/// <paramref name="angle"/> (0, 90, 180 or 270), with a footprint of <paramref name="width"/> × <paramref name="depth"/> cells.
	/// </summary>
	/// <param name="origin">The heightfield's world origin (heightfield +0x08/+0x0C; 0 or −0 in retail levels).</param>
	/// <param name="shape">The object's shape grid, or null (then every cell counts for the height).</param>
	/// <exception cref="ArgumentOutOfRangeException">The angle is not a quarter turn: the game handles only those four.</exception>
	public static PlacedInstance Place( Heightfield terrain, Vector2 origin, int cellX, int cellZ, int angle,
		int width, int depth, FootprintShape? shape, PlacementKind kind )
	{
		if ( angle is not (0 or 90 or 180 or 270) ) throw new ArgumentOutOfRangeException( nameof( angle ), angle, "Placement handles quarter turns only" );
		var (x, z, left, top) = FootprintCorner( cellX, cellZ, angle, width, depth );
		bool quarter = angle is 90 or 270;
		int extentX = quarter ? depth : width, extentZ = quarter ? width : depth;
		// MeshInstance_Create: without the footprint flag or a shape the origin corner is sampled directly.
		float height = shape == null || (kind & PlacementKind.FootprintHeight) == 0
			? Corner( terrain, x, z )
			: GroundHeight( terrain, left, top, width, depth, angle, shape, (kind & PlacementKind.AllCells) != 0 );
		var translation = new Vector3( x * terrain.CellSizeX + origin.X, height, z * terrain.CellSizeZ + origin.Y );
		return new PlacedInstance( x, z, left, top, extentX, extentZ, height, angle, translation, (kind & PlacementKind.Transform) != 0 );
	}

	/// <summary>
	/// MeshInstance_FootprintCorner (US 0x466f90 / TPI 0x4c9e00, identical bodies): moves the model origin to the corner of the
	/// footprint the turned model grows from (90: z+1; 180: x+1, z+1; 270: x+1) and returns that origin and the rectangle's
	/// lowest cell. The saved mTopLeft − 1 is the cell before this shift (all quarter turns in the Theme Park World samples:
	/// <c>shared\blocks\park-placement\evidence\topleft_check.txt</c>).
	/// </summary>
	public static (int X, int Z, int Left, int Top) FootprintCorner( int x, int z, int angle, int width, int depth ) => angle switch
	{
		90 => (x, z + 1, x, z + 1 - width),
		180 => (x + 1, z + 1, x + 1 - width, z + 1 - depth),
		270 => (x + 1, z, x + 1 - depth, z),
		_ => (x, z, x, z),
	};

	/// <summary>
	/// MeshInstance_GroundHeight (US 0x466d70 / TPI 0x4c9b60): the mean of the lowest and highest corner heights over the
	/// footprint's set cells. Each cell corner (u, v) of the unturned grid is mapped onto the turned rectangle by
	/// MeshInstance_FootprintPoint (US 0x454050 / TPI 0x4aa2c0), which adds 0.1 (constant at US 0x6fe47c, −0.1 subtracted)
	/// and truncates to a grid corner.
	/// </summary>
	public static float GroundHeight( Heightfield terrain, int left, int top, int width, int depth, int angle, FootprintShape? shape, bool allCells )
	{
		bool quarter = angle is 90 or 270;
		int right = left + (quarter ? depth : width), bottom = top + (quarter ? width : depth);
		// The game starts from ±10000 (US 0x466d70 locals), so an empty footprint averages to 0.
		float low = 10000, high = -10000;
		for ( int u = 0; u < width; u++ )
			for ( int v = 0; v < depth; v++ )
			{
				if ( !allCells && shape != null && !shape.IsSet( u, v ) ) continue;
				for ( int corner = 0; corner < 4; corner++ )
				{
					// Unit-square coordinates; the game accumulates 1/width and 1/depth in single precision.
					float fu = (float)u / width + ((corner & 1) != 0 ? 1f / width : 0);
					float fv = (float)v / depth + ((corner & 2) != 0 ? 1f / depth : 0);
					var (gx, gz) = FootprintPoint( angle, left, top, right, bottom, fu, fv );
					float h = Corner( terrain, gx, gz );
					low = MathF.Min( low, h );
					high = MathF.Max( high, h );
				}
			}
		return (low + high) * 0.5f;
	}

	/// <summary>
	/// MeshInstance_FootprintPoint (US 0x454050): unit-square (u, v) of the unturned footprint → grid corner of the rectangle
	/// (x0, z0)–(x1, z1), per quarter turn; truncated toward zero after adding 0.1.
	/// </summary>
	public static (int X, int Z) FootprintPoint( int angle, float x0, float z0, float x1, float z1, float u, float v )
	{
		static int Snap( float value ) => (int)(value + 0.1f);
		return angle switch
		{
			90 => (Snap( (1 - v) * x0 + v * x1 ), Snap( u * z0 + (1 - u) * z1 )),
			180 => (Snap( u * x0 + (1 - u) * x1 ), Snap( v * z0 + (1 - v) * z1 )),
			270 => (Snap( v * x0 + (1 - v) * x1 ), Snap( (1 - u) * z0 + u * z1 )),
			_ => (Snap( (1 - u) * x0 + u * x1 ), Snap( (1 - v) * z0 + v * z1 )),
		};
	}

	/// <summary>
	/// Cells of a turned footprint that the shape occupies, through the same corner shift and point map as the height.
	/// Matches the save map's type-4 cells in every Theme Park World sample (see <see cref="FootprintShape"/>).
	/// </summary>
	public static IEnumerable<(int X, int Z)> OccupiedCells( PlacedInstance placed, FootprintShape shape )
	{
		int right = placed.TopLeftX + placed.ExtentX, bottom = placed.TopLeftZ + placed.ExtentZ;
		for ( int u = 0; u < shape.Width; u++ )
			for ( int v = 0; v < shape.Depth; v++ )
				if ( shape.Occupies( u, v ) )
					// A cell centre maps to k + 0.5 inside the rectangle, so the 0.1 snap truncates it to that cell.
					yield return FootprintPoint( placed.AngleDegrees, placed.TopLeftX, placed.TopLeftZ, right, bottom,
						(u + 0.5f) / shape.Width, (v + 0.5f) / shape.Depth );
	}

	/// <summary>Height of a grid corner, clamped to the grid (the game indexes heights directly; clamping keeps edge objects in range).</summary>
	private static float Corner( Heightfield terrain, int x, int z ) =>
		terrain.HeightAt( Math.Clamp( x, 0, terrain.Width ), Math.Clamp( z, 0, terrain.Depth ) );

	/// <summary>
	/// Cosine and sine of a quarter turn. The game looks both up in its sine table (sin(a + π/2) and sin(a), US 0x7b0968);
	/// exact values are used here, the table's quarter-turn entries have not been read.
	/// </summary>
	public static (float Cos, float Sin) CosSin( int angle ) => ((angle % 360 + 360) % 360) switch
	{
		90 => (0, 1),
		180 => (-1, 0),
		270 => (0, -1),
		_ => (1, 0),
	};
}
