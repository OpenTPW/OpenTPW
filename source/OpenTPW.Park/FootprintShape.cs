namespace OpenTPW.Park;

/// <summary>
/// An object's <c>Info.Shape</c> grid from its <c>.sam</c>: the cells it covers at 0 degrees. Both games read the same
/// text block (keyword table US 0x744be4 / TPI 0x9a2cfc, type 11) and test the same per-cell flags
/// (Footprint_CellIsSet, US 0x469930 / TPI 0x4ce450).
/// </summary>
/// <remarks>
/// Cell (x, z) is in model order: x along a text line, z = 0 the last line. The first line is the far edge (largest z):
/// with that orientation and <see cref="Occupies"/>, all 57 multi-cell objects in the twelve Theme Park World sample saves
/// (0, 90 and 270 degrees) cover exactly the type-4 cells their mTopLeft owns on the save map
/// (<c>shared\blocks\park-placement\evidence\shape_check.py</c>). The game's text-to-flag parser has not been located.
/// </remarks>
public sealed class FootprintShape
{
	private readonly string[] rows;

	public int Width { get; }
	public int Depth { get; }

	public FootprintShape( IReadOnlyList<string> lines )
	{
		if ( lines.Count == 0 ) throw new InvalidDataException( "Empty Info.Shape block" );
		rows = lines.Select( line => line.TrimEnd() ).ToArray();
		Width = rows.Max( row => row.Length );
		Depth = rows.Length;
		if ( Width == 0 ) throw new InvalidDataException( "Info.Shape block has no cells" );
	}

	private char At( int x, int z )
	{
		if ( x < 0 || z < 0 || x >= Width || z >= Depth ) return '.';
		var row = rows[Depth - 1 - z];
		return x < row.Length ? row[x] : '.';
	}

	/// <summary>
	/// Whether the cell counts for the ground height (Footprint_CellIsSet tests the low four flag bits). Treating every
	/// character but '.' as set is an inference: the parser that turns characters into flags is not identified.
	/// </summary>
	public bool IsSet( int x, int z ) => At( x, z ) != '.';

	/// <summary>
	/// Whether the cell is the object's own footprint (save map type 4). '*', '&lt;' and '&gt;' are; entrance and exit
	/// markers such as 'S', '2', 'N' and 'E' are other cell types. Established from the sample saves (see the remarks).
	/// </summary>
	public bool Occupies( int x, int z ) => At( x, z ) is '*' or '<' or '>';
}
