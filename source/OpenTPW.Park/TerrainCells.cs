using Heightfield = OpenTPW.ModelFile.HeightfieldData;

namespace OpenTPW.Park;

// Inside the namespace this alias wins over OpenTPW.Vector2 from OpenTPW.Common.
using Vector2 = System.Numerics.Vector2;

/// <summary>A saved map cell's tile: its tile set, tile number and rotation, as MapCell_ApplyTile reads them.</summary>
/// <param name="Set">Tile set: 1 path (from the .tct table), 2 queue, 0 with tile 8 or 55 a special ground state.</param>
/// <param name="RotationDegrees">0, 90, 180 or 270.</param>
public readonly record struct MapTile( int X, int Z, int Set, int Tile, int RotationDegrees );

/// <summary>
/// The tile tables a level's .tct gives the terrain: one list of texture stems per tile set, in the order the game
/// passes the section names. Theme Park World reads <c>PathTex</c> and <c>QueueTex</c> (table US 0x74c8a8, read by
/// FUN_00427ad0); Theme Park Inc reads only <c>PathTex</c> (table TPI 0x9aded0, read by TPI 0x461000), renamed to
/// <c>SPathTex</c> at the lowest detail (TPI 0x6a13b0), a section no retail level has, so no path tile is written then.
/// Section names compare without case (Tct_Read, US 0x427620, uses _stricmp, US 0x67df40). The game loads each row from
/// <c>&lt;level&gt;\Terrain\&lt;section&gt;\</c> (Terrain_AddTctFrames, US 0x46e130), i.e. the <c>pathtex/</c> folder
/// of terrain.wad.
/// </summary>
public sealed class TileTables
{
	public IReadOnlyList<IReadOnlyList<string>> Sets { get; }

	public TileTables( TctFile tct, params string[] sectionNames ) =>
		Sets = sectionNames.Select( name => tct.SectionsInFileOrder
			.Where( pair => pair.Key.Equals( name, StringComparison.OrdinalIgnoreCase ) )
			.Select( pair => pair.Value ).FirstOrDefault() ?? [] ).ToArray();

	public TileTables( IReadOnlyList<IReadOnlyList<string>> sets ) => Sets = sets;
}

/// <summary>
/// A working copy of a level heightfield's cell attributes with the park's tiles written in, and how the terrain draws
/// each cell. Both games run the same code: Terrain_WriteCellTile (US 0x46df50 / TPI 0x4d7cf0) writes tiles,
/// Terrain_DrawCells (US 0x56f670 / TPI 0x6d0ee0) draws one textured quad per cell. That draw path is the one without
/// procedural textures, which both retail installs here run ("Using procedural textures: 0" in enginedebug.txt); the
/// procedural path (Terrain_ComposeCellTexture, US 0x55f780) blends base.lnd layers instead and is not modelled.
/// </summary>
public sealed class TerrainCells
{
	/// <summary>Bit 0: the terrain does not draw the cell (Terrain_DrawCells, Terrain_CollectCellTextures skip it).</summary>
	public const int Hidden = 0x01;
	/// <summary>Bit 1: the quad is split along the (x, z)–(x+1, z+1) diagonal instead of (x, z+1)–(x+1, z).</summary>
	public const int SplitThroughOrigin = 0x02;
	/// <summary>Texture turn written for a 270 degree tile: each corner takes the coordinate of the corner before it in (x, z), (x, z+1), (x+1, z+1), (x+1, z).</summary>
	public const int Turn270 = 0x08;
	/// <summary>Texture turn written for a 180 degree tile: each corner takes the opposite corner's coordinate.</summary>
	public const int Turn180 = 0x10;
	/// <summary>Texture turn written for a 90 degree tile: each corner takes the coordinate of the corner after it.</summary>
	public const int Turn90 = 0x20;
	/// <summary>Texture mirrored across the cell's (x, z)–(x+1, z+1) diagonal before turning.</summary>
	public const int Mirror = 0x40;

	/// <summary>The tile set 0 tile that hides the ground under queue pieces and buildings (Terrain_WriteCellTile).</summary>
	public const int HideTile = 8;
	/// <summary>The tile set 0 tile that restores the level's own cell (Terrain_WriteCellTile).</summary>
	public const int RestoreTile = 0x37;

	private readonly Heightfield terrain;
	private readonly IReadOnlyList<string> frameNames;
	private readonly TileTables tables;
	private readonly ushort[] flags;
	private readonly string[] textures;

	public int Width => terrain.Width;
	public int Depth => terrain.Depth;

	/// <param name="frameNames">The level model's frame list (<see cref="ModelFile.TextureNames"/>), which the cells' texture words index.</param>
	public TerrainCells( Heightfield terrain, IReadOnlyList<string> frameNames, TileTables tables )
	{
		this.terrain = terrain;
		this.frameNames = frameNames;
		this.tables = tables;
		flags = new ushort[terrain.Cells.Length];
		textures = new string[terrain.Cells.Length];
		for ( int i = 0; i < flags.Length; i++ ) Restore( i );
	}

	public int FlagsAt( int x, int z ) => flags[z * Width + x];
	public bool IsDrawn( int x, int z ) => (flags[z * Width + x] & Hidden) == 0;
	/// <summary>The ground texture stem; "" when the frame index is outside the model's frame list.</summary>
	public string TextureAt( int x, int z ) => textures[z * Width + x];

	/// <summary>
	/// MapCell_ApplyTile (US 0x5366e0 / TPI 0x67fce0) for every saved cell, as the game does after loading a save
	/// (US 0x4d83a0 / TPI 0x5d7320 over all 128 × 128 cells): tile set 2 hides the ground under its queue piece,
	/// everything else goes to <see cref="Write"/>.
	/// </summary>
	public void Apply( IEnumerable<MapTile> tiles )
	{
		foreach ( var tile in tiles )
		{
			if ( tile.Set == 2 ) Write( tile.X, tile.Z, 0, HideTile, 0 );
			else Write( tile.X, tile.Z, tile.Set, tile.Tile, tile.RotationDegrees );
		}
	}

	/// <summary>
	/// Terrain_WriteCellTile (US 0x46df50 / TPI 0x4d7cf0). Set 0 tile 8 hides the cell (flags |= 0x101, clears 0x80);
	/// set 0 tile 55 restores the level's cell. Otherwise set 0 counts as set 1: the texture becomes row
	/// <c>tile % count</c> of that set's table, the turn and mirror bits (0x78) and bit 0 are cleared, 0x180 is set and the
	/// rotation adds its turn bit. Cells outside the heightfield and sets with an empty table are left alone.
	/// </summary>
	public void Write( int x, int z, int set, int tile, int rotationDegrees )
	{
		bool hide = set == 0 && tile == HideTile, restore = set == 0 && tile == RestoreTile;
		IReadOnlyList<string>? rows = null;
		if ( !hide && !restore )
		{
			if ( set == 0 ) set = 1;
			if ( tables.Sets.Count < 1 ) return;
			// The game reads the count of the requested set before wrapping the set number; past the table end that read
			// lands outside it, so the wrapped set's count is used here. Retail saves only write sets 1 and 2.
			set = (set - 1) % tables.Sets.Count + 1;
			rows = tables.Sets[set - 1];
			if ( rows.Count < 1 ) return;
		}
		if ( x < 0 || x >= Width || z < 0 || z >= Depth ) return;
		int index = z * Width + x;
		if ( hide )
		{
			// 0x100 is the game's "texture changed" mark; 0x80 marks a path overlay for the procedural cache.
			flags[index] = (ushort)((flags[index] | 0x101) & ~0x80);
		}
		else if ( restore )
		{
			Restore( index );
			flags[index] |= 0x100;
		}
		else
		{
			textures[index] = rows![tile % rows.Count];
			int value = (flags[index] & ~0x78 & ~Hidden) | 0x180;
			value |= rotationDegrees switch { 90 => Turn90, 180 => Turn180, 270 => Turn270, _ => 0 };
			flags[index] = (ushort)value;
		}
	}

	// The game copies the cell back from a second copy of the level heightfield (US 0x79fc54). With procedural textures
	// on it also clears the turn bits; on the drawn path the level's own bits come back.
	private void Restore( int index )
	{
		uint cell = terrain.Cells[index];
		flags[index] = (ushort)cell;
		int frame = (int)(cell >> 16);
		textures[index] = frame < frameNames.Count ? frameNames[frame] : "";
	}

	/// <summary>
	/// Texture coordinates of the cell's corners (x, z), (x+1, z), (x+1, z+1), (x, z+1), as Terrain_DrawCells passes
	/// them: u along +x and v along +z for an unturned cell, then Terrain_CellUvOrder (US 0x56f4f0 / TPI 0x6d0d00)
	/// reorders the four corner slots by the cell's bits. Coordinates are the game's (v = 0 at the texture's first row
	/// as uploaded); a renderer that flips model V flips these the same way.
	/// </summary>
	public Vector2[] CornerUvs( int x, int z ) => CornerUvs( FlagsAt( x, z ) );

	public static Vector2[] CornerUvs( int cellFlags )
	{
		// The game numbers the corners (x, z), (x, z+1), (x+1, z+1), (x+1, z) with coordinates (0,0), (0,1), (1,1), (1,0)
		// and permutes which coordinate each corner slot takes.
		Vector2[] table = [new( 0, 0 ), new( 0, 1 ), new( 1, 1 ), new( 1, 0 )];
		int[] slot = [0, 1, 2, 3];
		if ( (cellFlags & 0x78) != 0 )
		{
			if ( (cellFlags & Mirror) != 0 ) (slot[1], slot[3]) = (slot[3], slot[1]);
			// One turn only, tested in this order.
			if ( (cellFlags & Turn270) != 0 ) slot = [slot[3], slot[0], slot[1], slot[2]];
			else if ( (cellFlags & Turn180) != 0 ) slot = [slot[2], slot[3], slot[0], slot[1]];
			else if ( (cellFlags & Turn90) != 0 ) slot = [slot[1], slot[2], slot[3], slot[0]];
		}
		// Back to the order (x, z), (x+1, z), (x+1, z+1), (x, z+1).
		return [table[slot[0]], table[slot[3]], table[slot[2]], table[slot[1]]];
	}

	/// <summary>
	/// Whether the quad's two triangles meet along (x, z)–(x+1, z+1). Terrain_DrawCells emits a four-vertex fan from
	/// corner (x, z) when bit 1 is set and from (x, z+1) otherwise.
	/// </summary>
	public bool SplitsThroughOrigin( int x, int z ) => (FlagsAt( x, z ) & SplitThroughOrigin) != 0;
}
