using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.Park;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Heightfield = OpenTPW.ModelFile.HeightfieldData;

namespace OpenTPW.Tests;

// Inside the namespace these aliases win over OpenTPW.Vector2/Vector3 from OpenTPW.Common.
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

[TestClass]
public class ParkPlacementTests
{
	/// <summary>A flat 10-unit grid whose corner heights are given by <paramref name="height"/>.</summary>
	private static Heightfield Grid( int width, int depth, Func<int, int, float> height )
	{
		var heights = new float[(width + 1) * (depth + 1)];
		for ( int z = 0; z <= depth; z++ )
			for ( int x = 0; x <= width; x++ )
				heights[z * (width + 1) + x] = height( x, z );
		return new Heightfield { Width = width, Depth = depth, CellSizeX = 10, CellSizeZ = 10, Heights = heights, Cells = new uint[width * depth] };
	}

	[TestMethod]
	[DataRow( 0, 10, 20, 10, 20 )]
	[DataRow( 90, 10, 21, 10, 18 )]
	[DataRow( 180, 11, 21, 8, 19 )]
	[DataRow( 270, 11, 20, 9, 20 )]
	public void FootprintCornerMovesTheOriginPerQuarterTurn( int angle, int x, int z, int left, int top )
	{
		// A 3 × 2 footprint whose model origin is saved at (10, 20).
		Assert.AreEqual( (x, z, left, top), Placement.FootprintCorner( 10, 20, angle, 3, 2 ) );
	}

	[TestMethod]
	public void QuarterTurnsSwapTheRectangleExtents()
	{
		var flat = Grid( 32, 32, ( _, _ ) => 0 );
		foreach ( int angle in new[] { 0, 90, 180, 270 } )
		{
			var placed = Placement.Place( flat, Vector2.Zero, 10, 20, angle, 3, 2, null, PlacementKind.Object );
			Assert.AreEqual( angle is 90 or 270 ? (2, 3) : (3, 2), (placed.ExtentX, placed.ExtentZ), $"{angle}" );
			// The saved cell is always inside the occupied rectangle.
			Assert.IsTrue( placed.Rectangle().Contains( (10, 20) ), $"{angle}" );
		}
		Assert.ThrowsExactly<ArgumentOutOfRangeException>( () => Placement.Place( flat, Vector2.Zero, 1, 1, 45, 1, 1, null, PlacementKind.Object ) );
	}

	[TestMethod]
	public void GroundHeightAveragesTheLowestAndHighestSetCorner()
	{
		// Height = x + 10·z, so the rectangle's corners span x + 10·z over its own range.
		var slope = Grid( 16, 16, ( x, z ) => x + 10 * z );
		var shape = new FootprintShape( ["**", "*."] ); // the cell at x = 1, z = 0 is empty
		// 0 degrees at (4, 4): set cells (0,0), (0,1), (1,1) span corners x 4..6, z 4..6 except none is lost: corner (6,4)
		// belongs to the empty cell only, so the lowest is (4,4) = 44 and the highest (6,6) = 66.
		var placed = Placement.Place( slope, Vector2.Zero, 4, 4, 0, 2, 2, shape, PlacementKind.Object );
		Assert.AreEqual( (44 + 66) / 2f, placed.Height );
		// Without the shape test (kind bit 0x10) every cell counts; here that gives the same extremes.
		Assert.AreEqual( (44 + 66) / 2f, Placement.GroundHeight( slope, 4, 4, 2, 2, 0, shape, allCells: true ) );

		// A shape whose only set cell is the far corner: the near corners must not count.
		var far = new FootprintShape( ["*.", ".."] ); // set cell x = 0, z = 1
		Assert.AreEqual( (54 + 65) / 2f, Placement.GroundHeight( slope, 4, 4, 2, 2, 0, far, allCells: false ) );
		Assert.AreEqual( (44 + 66) / 2f, Placement.GroundHeight( slope, 4, 4, 2, 2, 0, far, allCells: true ) );
	}

	[TestMethod]
	public void ObjectsWithoutAShapeOrTheFootprintBitTakeTheOriginCorner()
	{
		var slope = Grid( 16, 16, ( x, z ) => x + 10 * z );
		var shape = new FootprintShape( ["**", "**"] );
		// Kind 0x361 (fixed items) lacks bit 8: origin corner after the shift. At 180 degrees (4, 4) moves to (5, 5).
		Assert.AreEqual( 55f, Placement.Place( slope, Vector2.Zero, 4, 4, 180, 2, 2, shape, PlacementKind.Fixed ).Height );
		// No shape: origin corner as well, even for ordinary objects.
		Assert.AreEqual( 44f, Placement.Place( slope, Vector2.Zero, 4, 4, 0, 2, 2, null, PlacementKind.Object ).Height );
	}

	[TestMethod]
	public void FixedItemsUseTheEngineOffsetOrTheLevelFixedOrigin()
	{
		var flat = Grid( 128, 128, ( _, _ ) => 0 );
		static ObjectType Type( int x, int z ) => new( new SamInfo( 1, "", null, x, z, 0, 0, true ), "", "", 1, 1 );
		var saved = new SavedObject( 1, 1, 0 );
		var both = ObjectPlacement.Place( flat, Vector2.Zero, Type( 47, 17 ), saved, (5, 6) )!;
		Assert.AreEqual( (47, 17), (both.OriginX, both.OriginZ) );
		// Each axis falls back on its own when its override is 0.
		var fallback = ObjectPlacement.Place( flat, Vector2.Zero, Type( 0, 17 ), saved, (5, 6) )!;
		Assert.AreEqual( (5, 17), (fallback.OriginX, fallback.OriginZ) );
		// Ordinary objects off the map are not placed.
		var ordinary = new ObjectType( new SamInfo( 2, "", null, 0, 0, 0, 0, false ), "", "", 1, 1 );
		Assert.IsNull( ObjectPlacement.Place( flat, Vector2.Zero, ordinary, new SavedObject( 2, 0, 0 ), (5, 6) ) );
		// mTopLeft − 1 = z × 128 + x.
		Assert.AreEqual( (37, 23), new SavedObject( 2, 2982, 270 ).Cell );
	}

	[TestMethod]
	public void RootLocalTurnsTheBasisAndReplacesTheTranslation()
	{
		var hill = Grid( 16, 16, ( _, _ ) => 3 );
		var placed = Placement.Place( hill, new Vector2( 1, 2 ), 4, 5, 90, 1, 1, null, PlacementKind.Object );
		Assert.AreEqual( new Vector3( 4 * 10 + 1, 3, 6 * 10 + 2 ), placed.Translation );
		var authored = Matrix4x4.CreateScale( 2 ) * Matrix4x4.CreateTranslation( 29.5f, 1.1f, 56.4f );
		var root = placed.RootLocal( authored );
		// Row vectors: the model's +x axis turns to −z at 90 degrees (x' = x·c + z·s, z' = z·c − x·s).
		var axis = Vector3.TransformNormal( Vector3.UnitX, root );
		Assert.AreEqual( 0, axis.X, 1e-6f );
		Assert.AreEqual( -2, axis.Z, 1e-6f );
		Assert.AreEqual( placed.Translation, root.Translation );
	}

	[TestMethod]
	[DataRow( 0, 0 )]
	[DataRow( 90, 270 )]
	[DataRow( 180, 180 )]
	[DataRow( 270, 90 )]
	public void QueuePiecesTurnByThreeSixtyMinusTheCellRotation( int rotation, int angle )
	{
		Assert.AreEqual( angle, QueuePieces.Angle( rotation ) );
		var placed = QueuePieces.Place( Grid( 8, 8, ( x, z ) => x + 10 * z ), Vector2.Zero, new QueueCell( 2, 2, 2, rotation ) );
		// One cell, height from all four of its corners after the corner shift.
		Assert.AreEqual( (1, 1), (placed.ExtentX, placed.ExtentZ) );
		Assert.AreEqual( (22 + 33) / 2f, placed.Height );
	}

	[TestMethod]
	public void TheFirstRegistrationOfAnIdWins()
	{
		var catalog = new ObjectCatalog();
		var first = new ObjectType( new SamInfo( 7, "first", null, 0, 0, 0, 0, false ), "a.wad", "a.md2", 1, 1 );
		var second = first with { Source = "b.wad" };
		Assert.IsTrue( catalog.Add( first ) );
		Assert.IsFalse( catalog.Add( second ) );
		Assert.AreSame( first, catalog.Find( 7 ) );
		CollectionAssert.AreEqual( new[] { second }, catalog.Duplicates.ToArray() );
	}

	[TestMethod]
	public void SamInfoReadsKeysByNameAndTheShapeBlock()
	{
		const string text = "#\tcomment Info.Id 99\r\nInfo.Id\t\t1403\r\nInfo.Type\t\t2\r\nInfo.Name\t\"Fountain\"\r\nInfo.Shape\r\n---\r\n**.\r\n***\r\n---\r\n"
			+ "Info.EngineMapOffsetOverrideX\t47\r\nInfo.DontApplyOffset\t1\t\tcomment\r\n";
		var info = SamInfo.Parse( text )!;
		Assert.AreEqual( 1403, info.Id );
		Assert.AreEqual( "Fountain", info.Name );
		Assert.AreEqual( (47, 0), (info.EngineMapOffsetOverrideX, info.EngineMapOffsetOverrideZ) );
		Assert.IsTrue( info.DontApplyOffset );
		// The first line is the far edge: (2, 1) is the '.'.
		Assert.AreEqual( (3, 2), (info.Shape!.Width, info.Shape.Depth) );
		Assert.IsFalse( info.Shape.IsSet( 2, 1 ) );
		Assert.IsTrue( info.Shape.IsSet( 2, 0 ) );
		Assert.IsNull( SamInfo.Parse( "Info.Name \"no id\"" ) );
	}

	/// <summary>
	/// Theme Park World data: the jungle fountain, staff room and balloon shop, turned 90, 90 and 270 degrees in
	/// <c>samples\saves\tw_jungle_save_coaster.TPWS</c>. Expected cells are the save map's type-4 cells owned by each mTopLeft
	/// (<c>shared\blocks\park-placement\evidence\topleft_check.py</c>; every sample matches, see shape_check.txt there).
	/// </summary>
	[TestMethod]
	[DataRow( "features/fountain.wad", 1403, 2618, 90, "57,18 57,19 57,20 58,18 58,19 58,20 59,18 59,19 59,20" )]
	[DataRow( "features/staff.wad", 1411, 2235, 90, "58,17 59,16 59,17" )]
	[DataRow( "shops/balloon.wad", 1209, 2982, 270, "35,23 35,24 35,25 36,23 36,24 36,25 37,23 37,25" )]
	public void TurnedThemeParkWorldObjectsCoverTheirSavedFootprint( string archive, int id, int topLeft, int angle, string cells )
	{
		GameData.Require();
		var info = ReadSam( Path.Combine( GameData.GamePath!, "data", "levels", "jungle", archive ) ).Single( sam => sam.Id == id );
		Assert.IsNotNull( info.Shape );
		var flat = Grid( 128, 128, ( _, _ ) => 0 );
		var saved = new SavedObject( id, topLeft, angle );
		var type = new ObjectType( info, archive, "", info.Shape.Width, info.Shape.Depth );
		var placed = ObjectPlacement.Place( flat, Vector2.Zero, type, saved, (0, 0) )!;
		var expected = cells.Split( ' ' ).Select( pair => pair.Split( ',' ) ).Select( p => (int.Parse( p[0] ), int.Parse( p[1] )) ).OrderBy( c => c ).ToArray();
		CollectionAssert.AreEqual( expected, Placement.OccupiedCells( placed, info.Shape ).OrderBy( c => c ).ToArray() );
	}

	/// <summary>Every object definition at the root of a level archive.</summary>
	internal static IEnumerable<SamInfo> ReadSam( string wadPath )
	{
		using var wad = new WadArchive( wadPath );
		foreach ( var child in wad.Root.Children )
		{
			if ( child is not WadArchiveFile file || !child.Name!.EndsWith( ".sam", StringComparison.OrdinalIgnoreCase ) ) continue;
			try
			{
				// .sam text is 8-bit; Latin-1 keeps every byte.
				if ( SamInfo.Parse( Encoding.Latin1.GetString( file.GetData() ) ) is { } info ) yield return info;
			}
			finally { file.Free(); }
		}
	}
}
