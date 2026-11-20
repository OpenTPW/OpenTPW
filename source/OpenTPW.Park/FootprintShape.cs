namespace OpenTPW.Park;

/// <summary>
/// How a game reads an object's <c>Info.Shape</c> text: each character's cell value (the word that Footprint_CellIsSet,
/// US 0x469930 / TPI 0x4ce450, tests) and the largest grid its parser accepts. The <c>Shape</c> keyword (type 11, keyword
/// table entry +0x38 = 1) selects table 1 of Sam_Parse (US 0x4017a0 / TPI 0x419cd0); the games' tables and grid limits
/// differ, so the host picks one.
/// </summary>
/// <remarks>
/// Only the value word matters for placement. The table also gives each character a direction byte (for example the
/// entrance markers 'N', 'E', 'S' and 'W'), which is not kept here.
/// </remarks>
public sealed class ShapeCharacters
{
	private readonly Dictionary<char, uint> values;

	private ShapeCharacters( int maxCells, Dictionary<char, uint> values )
	{
		MaxCells = maxCells;
		this.values = values;
	}

	/// <summary>Cells a line and lines a grid that Sam_ParseMap accepts ("Map too wide" / "Map too high").</summary>
	public int MaxCells { get; }

	/// <summary>
	/// Theme Park World: 19 entries at US 0x7396c8 (12 bytes each: character, value word, direction byte); a 20 × 20 grid
	/// (US 0x402720 stops at 20 cells a line and at 400 cells).
	/// </summary>
	public static ShapeCharacters ThemeParkWorld { get; } = new( 20, new()
	{
		['.'] = 0x00, ['*'] = 0x04, ['Q'] = 0x03, ['@'] = 0x01, ['+'] = 0x0b, ['#'] = 0x10,
		['8'] = 0x09, ['6'] = 0x09, ['2'] = 0x09, ['4'] = 0x09,
		['N'] = 0x0a, ['E'] = 0x0a, ['S'] = 0x0a, ['W'] = 0x0a,
		['D'] = 0x17, ['>'] = 0x17, ['<'] = 0x17, ['O'] = 0x09, ['X'] = 0x0a,
	} );

	/// <summary>
	/// Theme Park Inc: 24 entries at TPI 0x98df88. Adds 'H', ',', '=' and '%', and gives 'X' and 'O' new values. A 40 × 40
	/// grid (TPI 0x41ad90 stops at 40 cells a line and at 1600 cells); the science lake's shape has 37 lines.
	/// </summary>
	public static ShapeCharacters ThemeParkInc { get; } = new( 40, new()
	{
		['.'] = 0x00, ['*'] = 0x04, ['Q'] = 0x03, ['@'] = 0x01, ['+'] = 0x0b, ['#'] = 0x10,
		['H'] = 0x08, ['X'] = 0x05, [','] = 0x1e, ['='] = 0x20, ['%'] = 0x21,
		['8'] = 0x09, ['6'] = 0x09, ['2'] = 0x09, ['4'] = 0x09,
		['N'] = 0x0a, ['E'] = 0x0a, ['S'] = 0x0a, ['W'] = 0x0a,
		['D'] = 0x17, ['>'] = 0x17, ['<'] = 0x17, ['O'] = 0x1b, ['I'] = 0x1b,
	} );

	/// <summary>The cell value of <paramref name="c"/>, or false for a character the game rejects ("Illegal map character").</summary>
	public bool TryGetValue( char c, out uint value ) => values.TryGetValue( c, out value );
}

/// <summary>
/// An object's <c>Info.Shape</c> grid from its <c>.sam</c>: the cells it covers at 0 degrees, read as
/// Sam_ParseMap (US 0x402720 / TPI 0x41ad90) does.
/// </summary>
/// <remarks>
/// <para>
/// The parser skips spaces, looks every other character up in the <see cref="ShapeCharacters"/> table and stops on an
/// unknown one, allows at most <see cref="ShapeCharacters.MaxCells"/> cells a line and lines, takes the longest line as the width (shorter lines end in
/// value-0 cells), and then reverses the lines. So cell (x, z) is in model order: x along a line, z = 0 the last line, and
/// the first line is the far edge (largest z).
/// </para>
/// <para>
/// With that orientation and <see cref="Occupies"/>, all 57 multi-cell objects in the twelve Theme Park World sample saves
/// (0, 90 and 270 degrees) cover exactly the type-4 cells their mTopLeft owns on the save map
/// (<c>shared\blocks\park-placement\evidence\shape_check.py</c>).
/// </para>
/// </remarks>
public sealed class FootprintShape
{
	private readonly char[][] rows;
	private readonly uint[][] values;

	public int Width { get; }
	public int Depth { get; }

	/// <exception cref="InvalidDataException">The grid is empty, too large, or uses a character the table does not have.</exception>
	public FootprintShape( IReadOnlyList<string> lines, ShapeCharacters characters )
	{
		rows = lines.Select( line => line.Where( c => c != ' ' ).ToArray() ).ToArray();
		if ( rows.Length == 0 || rows.All( row => row.Length == 0 ) ) throw new InvalidDataException( "Info.Shape block has no cells" );
		int maxCells = characters.MaxCells;
		if ( rows.Length > maxCells ) throw new InvalidDataException( $"Info.Shape has {rows.Length} lines; the game allows {maxCells}" );
		values = new uint[rows.Length][];
		for ( int r = 0; r < rows.Length; r++ )
		{
			if ( rows[r].Length > maxCells ) throw new InvalidDataException( $"Info.Shape line {r + 1} has {rows[r].Length} cells; the game allows {maxCells}" );
			values[r] = rows[r].Select( c => characters.TryGetValue( c, out uint value ) ? value
				: throw new InvalidDataException( $"Info.Shape uses '{c}', which the game rejects" ) ).ToArray();
		}
		Width = rows.Max( row => row.Length );
		Depth = rows.Length;
	}

	private (char Char, uint Value) At( int x, int z )
	{
		if ( x < 0 || z < 0 || x >= Width || z >= Depth ) return ('.', 0);
		int r = Depth - 1 - z;
		return x < rows[r].Length ? (rows[r][x], values[r][x]) : ('.', 0);
	}

	/// <summary>The cell's value word from the character table (0 outside the grid).</summary>
	public uint ValueAt( int x, int z ) => At( x, z ).Value;

	/// <summary>
	/// Whether the cell counts for the ground height: Footprint_CellIsSet tests the low four bits of the value, so '.' and
	/// '#' (0x10), and in Theme Park Inc also '=' (0x20), do not.
	/// </summary>
	public bool IsSet( int x, int z ) => (ValueAt( x, z ) & 0xf) != 0;

	/// <summary>
	/// Whether the cell is the object's own footprint (save map type 4). '*', '&lt;' and '&gt;' are; entrance and exit
	/// markers such as 'S', '2', 'N' and 'E' are other cell types. Established from the sample saves (see the remarks);
	/// the code that writes the save map from these cells has not been traced.
	/// </summary>
	public bool Occupies( int x, int z ) => At( x, z ).Char is '*' or '<' or '>';
}
