using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.Park;
using System;
using System.Collections.Generic;
using System.Linq;
using Heightfield = OpenTPW.ModelFile.HeightfieldData;
using Vector2 = System.Numerics.Vector2;

namespace OpenTPW.Tests;

[TestClass]
public class ParkTerrainTests
{
	private static readonly string[] ThemeParkWorldSets = ["PathTex", "QueueTex"];

	/// <summary>A 3 × 2 heightfield whose cells name frames 0…5 and carry some level bits.</summary>
	private static (Heightfield Terrain, string[] Frames) SmallLevel()
	{
		var terrain = new Heightfield { Width = 3, Depth = 2, CellSizeX = 10, CellSizeZ = 10, Heights = new float[12] };
		terrain.Cells = Enumerable.Range( 0, 6 ).Select( i => (uint)(i << 16) | (i == 4 ? 0x1u : 0x2u | 0x40u) ).ToArray();
		return (terrain, Enumerable.Range( 0, 6 ).Select( i => $"frame{i}" ).ToArray());
	}

	private static TileTables Tables( params string[][] sets ) => new( sets );

	[TestMethod]
	[DataRow( 0, 0x00 )]
	[DataRow( 90, 0x20 )]
	[DataRow( 180, 0x10 )]
	[DataRow( 270, 0x08 )]
	public void PathTilesTakeTheTableRowAndTheTurnBit( int rotation, int turn )
	{
		var (terrain, frames) = SmallLevel();
		var cells = new TerrainCells( terrain, frames, Tables( ["p0", "p1", "p2"], ["q0"] ) );
		cells.Write( 1, 0, 1, 7, rotation );
		Assert.AreEqual( "p1", cells.TextureAt( 1, 0 ), "row tile % count" );
		// The level's 0x40 and the hidden bit are cleared, 0x180 set, bit 1 (the split) kept.
		Assert.AreEqual( 0x180 | 0x02 | turn, cells.FlagsAt( 1, 0 ) );
		Assert.IsTrue( cells.IsDrawn( 1, 0 ) );
	}

	[TestMethod]
	public void SetZeroCountsAsSetOneAndSetsWrapAroundTheTable()
	{
		var (terrain, frames) = SmallLevel();
		var worldTables = new TerrainCells( terrain, frames, Tables( ["p0", "p1"], ["q0", "q1"] ) );
		worldTables.Write( 0, 0, 0, 1, 0 );
		worldTables.Write( 1, 0, 2, 1, 0 );
		Assert.AreEqual( "p1", worldTables.TextureAt( 0, 0 ) );
		Assert.AreEqual( "q1", worldTables.TextureAt( 1, 0 ), "Theme Park World's set 2 is QueueTex" );

		var incTables = new TerrainCells( terrain, frames, Tables( ["p0", "p1"] ) );
		incTables.Write( 1, 0, 2, 1, 0 );
		Assert.AreEqual( "p1", incTables.TextureAt( 1, 0 ), "Theme Park Inc has one table, so set 2 wraps to PathTex" );
	}

	[TestMethod]
	public void EmptyTablesAndCellsOffTheGridAreLeftAlone()
	{
		var (terrain, frames) = SmallLevel();
		var cells = new TerrainCells( terrain, frames, Tables( [] ) );
		cells.Write( 0, 0, 1, 0, 90 );
		Assert.AreEqual( "frame0", cells.TextureAt( 0, 0 ) );
		Assert.AreEqual( 0x42, cells.FlagsAt( 0, 0 ) );
		var withRows = new TerrainCells( terrain, frames, Tables( ["p0"] ) );
		withRows.Write( 3, 0, 1, 0, 0 );
		withRows.Write( 0, -1, 1, 0, 0 );
		Assert.IsTrue( Enumerable.Range( 0, 6 ).All( i => withRows.TextureAt( i % 3, i / 3 ) == frames[i] ) );
	}

	[TestMethod]
	public void QueueAndBuildingCellsHideTheGroundAndTileFiftyFiveRestoresIt()
	{
		var (terrain, frames) = SmallLevel();
		var cells = new TerrainCells( terrain, frames, Tables( ["p0"] ) );
		cells.Write( 2, 1, 1, 0, 180 );
		Assert.AreEqual( 0x190 | 0x02, cells.FlagsAt( 2, 1 ) );

		cells.Write( 2, 1, 0, TerrainCells.HideTile, 0 );
		Assert.IsFalse( cells.IsDrawn( 2, 1 ) );
		Assert.AreEqual( 0x111 | 0x02, cells.FlagsAt( 2, 1 ), "0x101 set, the path overlay bit 0x80 cleared" );

		cells.Write( 2, 1, 0, TerrainCells.RestoreTile, 0 );
		Assert.AreEqual( "frame5", cells.TextureAt( 2, 1 ) );
		Assert.AreEqual( 0x142, cells.FlagsAt( 2, 1 ), "the level's own bits, with 0x100 marking the change" );
		Assert.IsTrue( cells.IsDrawn( 2, 1 ) );
	}

	[TestMethod]
	public void SavedTileSetTwoHidesTheGroundUnderItsQueuePiece()
	{
		var (terrain, frames) = SmallLevel();
		var cells = new TerrainCells( terrain, frames, Tables( ["p0"], ["q0"] ) );
		cells.Apply( [new MapTile( 0, 0, 2, 3, 90 ), new MapTile( 1, 0, 1, 0, 270 ), new MapTile( 2, 0, 0, TerrainCells.RestoreTile, 0 )] );
		Assert.IsFalse( cells.IsDrawn( 0, 0 ) );
		Assert.AreEqual( "frame0", cells.TextureAt( 0, 0 ) );
		Assert.AreEqual( "p0", cells.TextureAt( 1, 0 ) );
		Assert.AreEqual( "frame2", cells.TextureAt( 2, 0 ) );
	}

	[TestMethod]
	public void LevelCellsWithBitZeroAreNotDrawn()
	{
		var (terrain, frames) = SmallLevel();
		var cells = new TerrainCells( terrain, frames, Tables( ["p0"] ) );
		Assert.IsFalse( cells.IsDrawn( 1, 1 ) );
		Assert.IsTrue( cells.IsDrawn( 0, 1 ) );
		Assert.IsTrue( cells.SplitsThroughOrigin( 0, 1 ) );
		Assert.IsFalse( cells.SplitsThroughOrigin( 1, 1 ) );
	}

	/// <summary>
	/// Terrain_CellUvOrder (US 0x56f4f0): corner coordinates in the order (x, z), (x+1, z), (x+1, z+1), (x, z+1).
	/// </summary>
	[TestMethod]
	[DataRow( 0x00, new float[] { 0, 0, 1, 0, 1, 1, 0, 1 } )]
	[DataRow( 0x20, new float[] { 0, 1, 0, 0, 1, 0, 1, 1 } )]
	[DataRow( 0x10, new float[] { 1, 1, 0, 1, 0, 0, 1, 0 } )]
	[DataRow( 0x08, new float[] { 1, 0, 1, 1, 0, 1, 0, 0 } )]
	[DataRow( 0x40, new float[] { 0, 0, 0, 1, 1, 1, 1, 0 } )]
	[DataRow( 0x60, new float[] { 1, 0, 0, 0, 0, 1, 1, 1 } )]
	// Only the first turn bit counts: 8 before 0x10 before 0x20.
	[DataRow( 0x38, new float[] { 1, 0, 1, 1, 0, 1, 0, 0 } )]
	public void CornerCoordinatesFollowTheTurnAndMirrorBits( int flags, float[] expected )
	{
		var uvs = TerrainCells.CornerUvs( flags );
		CollectionAssert.AreEqual( expected, uvs.SelectMany( uv => new[] { uv.X, uv.Y } ).ToArray() );
	}

	[TestMethod]
	public void TablesKeepTheFileOrderAndTheGamesSectionOrder()
	{
		var tct = new TctFile( "; note\nQueueTex\n1 q1.tga\n0 q0.tga\nPathTex\n0 p0.tga\nOther\n0 o.tga\n" );
		var tables = new TileTables( tct, ThemeParkWorldSets );
		CollectionAssert.AreEqual( new[] { "p0" }, tables.Sets[0].ToArray() );
		CollectionAssert.AreEqual( new[] { "q1", "q0" }, tables.Sets[1].ToArray(), "rows by position, not by number" );
		Assert.AreEqual( 0, new TileTables( tct, "SPathTex" ).Sets[0].Count );
	}

	/// <summary>
	/// Theme Park World data: every level's drawn cells name a frame of its base.md2, and every PathTex row is a texture in
	/// terrain.wad's <c>pathtex</c> folder, where Terrain_AddTctFrames loads it, except the three marker textures
	/// (rows 16–18) no level ships and hallow's <c>jgr_bas1</c>, a jungle ground texture its table also lists and only its
	/// <c>textures</c> folder has. QueueTex rows are not checked: saved queue cells hide the ground rather than take a QueueTex texture.
	/// </summary>
	[TestMethod]
	public void EveryLevelResolvesItsGroundAndPathTextures()
	{
		GameData.Require();
		var levels = FileSystem.GetDirectories( "levels" ).Select( x => System.IO.Path.GetFileName( x ) )
			.Where( x => FileSystem.GetDirectories( $"levels/{x}" ).Any( d => System.IO.Path.GetFileName( d ).Equals( "terrain", StringComparison.OrdinalIgnoreCase ) ) ).ToList();
		Assert.IsTrue( levels.Count > 0 );
		foreach ( var level in levels )
		{
			var model = new ModelFile( FileSystem.OpenRead( $"levels/{level}/terrain/base.md2" ) );
			var tctPath = FileSystem.GetFiles( $"levels/{level}/terrain" ).Single( x => x.EndsWith( ".tct", StringComparison.OrdinalIgnoreCase ) );
			var tables = new TileTables( new TctFile( FileSystem.OpenRead( tctPath ) ), ThemeParkWorldSets );
			Assert.IsTrue( tables.Sets.All( set => set.Count > 0 ), level );

			var cells = new TerrainCells( model.Heightfield!, model.TextureNames, tables );
			for ( int z = 0; z < cells.Depth; z++ )
				for ( int x = 0; x < cells.Width; x++ )
					if ( cells.IsDrawn( x, z ) )
						Assert.AreNotEqual( "", cells.TextureAt( x, z ), $"{level} ({x}, {z})" );

			var folder = FileSystem.GetDirectories( $"levels/{level}/terrain" ).Single( d => System.IO.Path.GetFileName( d ).Equals( "pathtex", StringComparison.OrdinalIgnoreCase ) );
			var shipped = FileSystem.GetFiles( folder ).Select( System.IO.Path.GetFileNameWithoutExtension ).ToHashSet( StringComparer.OrdinalIgnoreCase );
			var missing = tables.Sets[0].Where( stem => !shipped.Contains( stem! ) ).ToHashSet();
			Assert.IsTrue( missing.IsSubsetOf( ["baseblue", "grnarrow", "redarrow", "jgr_bas1"] ), $"{level}: {string.Join( ", ", missing )}" );
		}
	}
}
