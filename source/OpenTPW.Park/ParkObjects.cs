using Heightfield = OpenTPW.ModelFile.HeightfieldData;

namespace OpenTPW.Park;

/// <summary>A placed object as a save records it; each game's save reader supplies these (see <see cref="IParkSave"/>).</summary>
/// <param name="TopLeft">The saved mTopLeft: 1 + z × 128 + x of the model origin cell before the corner shift; 0 when not on the map.</param>
/// <param name="AngleDegrees">0, 90, 180 or 270.</param>
public readonly record struct SavedObject( int TypeId, int TopLeft, int AngleDegrees )
{
	/// <summary>
	/// ParkObject_CreateMesh (US 0x4dd500 / TPI 0x5df740): index = mTopLeft − 1, x = index &amp; 0x7f, z = index &gt;&gt; 7.
	/// </summary>
	public (int X, int Z) Cell => ((TopLeft - 1) & 0x7f, (TopLeft - 1) >> 7);
}

/// <summary>A queue cell of the save map: tile set 2's tile number picks the piece, its rotation turns it.</summary>
public readonly record struct QueueCell( int X, int Z, int Tile, int RotationDegrees );

/// <summary>What placement needs from a save; each game adapts its own save reader.</summary>
public interface IParkSave
{
	IReadOnlyList<SavedObject> Objects { get; }
	IEnumerable<QueueCell> QueueCells { get; }
}

/// <summary>Places saved park objects (ParkObject_CreateMesh, US 0x4dd500 / TPI 0x5df740).</summary>
public static class ObjectPlacement
{
	/// <summary>
	/// Places <paramref name="saved"/> of <paramref name="type"/>. Fixed items (<c>DontApplyOffset</c>) go to
	/// <c>EngineMapOffsetOverrideX/Y</c>, each falling back to the level's <c>FixedItemOriginX/Y</c> when 0
	/// (globals US 0x785594/98, TPI 0xa025ac/b0), and take the origin corner's height (kind 0x361 lacks the footprint bit);
	/// other objects use the saved cell and the footprint height. The saved angle is used as is.
	/// </summary>
	/// <returns>Null for a non-fixed object that is not on the map (mTopLeft 0).</returns>
	public static PlacedInstance? Place( Heightfield terrain, ObjectType type, SavedObject saved, (int X, int Z) fixedItemOrigin )
	{
		if ( type.IsFixed )
		{
			int x = type.Info.EngineMapOffsetOverrideX != 0 ? type.Info.EngineMapOffsetOverrideX : fixedItemOrigin.X;
			int z = type.Info.EngineMapOffsetOverrideZ != 0 ? type.Info.EngineMapOffsetOverrideZ : fixedItemOrigin.Z;
			return Placement.Place( terrain, x, z, saved.AngleDegrees, type.Width, type.Depth, type.Info.Shape, PlacementKind.Fixed );
		}
		if ( saved.TopLeft <= 0 ) return null;
		var (cellX, cellZ) = saved.Cell;
		return Placement.Place( terrain, cellX, cellZ, saved.AngleDegrees, type.Width, type.Depth, type.Info.Shape, PlacementKind.Object );
	}
}

/// <summary>Queue pieces drawn on queue cells (Queue_LoadPieceModels / Queue_PlacePiece, identical in both games).</summary>
public static class QueuePieces
{
	/// <summary>
	/// The eight-entry piece table (US 0x763388 / TPI 0x9d6048; 12-byte entries: handle, name, flags), indexed by the cell's tile number.
	/// </summary>
	public static readonly IReadOnlyList<string> Models = ["quedead", "quedead", "questra", "quebnd2", "quebnd1", "queend", "quebin1", "quebin2"];

	/// <summary>Queue_PlacePiece (US 0x5229e0 / TPI 0x65cea0): angle = 360 − the cell rotation, with 360 taken as 0.</summary>
	public static int Angle( int rotationDegrees ) => 360 - rotationDegrees is var angle && angle == 360 ? 0 : angle;

	/// <summary>
	/// Queue_PlacePiece passes a static one-cell shape (US 0x816d68 / TPI 0xaf7548). Queue_LoadPieceModels (US 0x522900) sets
	/// only its width and depth to 1, so its cell value stays 0 and is not "set"; the cell still counts because kind 0x33a
	/// has the all-cells bit, so the height is the footprint rule over that cell's four corners.
	/// </summary>
	private static readonly FootprintShape OneCell = new( ["."], ShapeCharacters.ThemeParkWorld );

	public static PlacedInstance Place( Heightfield terrain, QueueCell cell ) =>
		Placement.Place( terrain, cell.X, cell.Z, Angle( cell.RotationDegrees ), 1, 1, OneCell, PlacementKind.QueuePiece );
}
