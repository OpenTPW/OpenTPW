using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenTPW.Park;

/// <summary>
/// The <c>Info.*</c> keys of an object's <c>.sam</c> that placement uses. Both games describe the definition with a keyword
/// table (US 0x744b30 / TPI 0x9a2cc0; 0x3C-byte entries: type, name, defaults; Theme Park Inc adds <c>Info.Type</c> after
/// <c>Info.Id</c>), so values are looked up by key name and the extra key needs no parameter.
/// </summary>
/// <remarks>
/// SamLoader_BindFields (US 0x401030) gives the n-th value key (types 4 to 11, n from 1) the address base + 4n, the base
/// being the loader's second virtual: definition +0xC for object definitions (loader at +4, ObjectDef_Construct
/// US 0x412c20 / TPI 0x43c210; virtual US 0x5b0d60 / TPI 0x43c2d0 returns loader +8). That puts Shape at +0x18,
/// EngineMapOffsetOverrideX/Y at +0x20/+0x24, EngineFootprintWidth/HeightOverride at +0x28/+0x2C and DontApplyOffset at +0x38
/// (Theme Park Inc one word later), the offsets ParkObject_CreateMesh (US 0x4dd500 / TPI 0x5df740) reads
/// (<c>shared\blocks\park-placement\evidence\descriptor_offsets.py</c>).
/// </remarks>
public sealed record SamInfo( int Id, string Name, FootprintShape? Shape, int EngineMapOffsetOverrideX, int EngineMapOffsetOverrideZ,
	int EngineFootprintWidthOverride, int EngineFootprintHeightOverride, bool DontApplyOffset )
{
	// Keys are matched at a line start so commented-out or embedded mentions are not taken.
	private static readonly Regex Key = new( @"^Info\.(\w+)[ \t]*([^\r\n]*)", RegexOptions.Multiline | RegexOptions.CultureInvariant );
	private static readonly Regex ShapeBlock = new( @"^Info\.Shape[^\r\n]*\r?\n---[ \t]*\r?\n(.*?)\r?\n---", RegexOptions.Multiline | RegexOptions.Singleline );

	/// <summary>Reads the keys from <c>.sam</c> text; null when it declares no <c>Info.Id</c> (not an object definition).</summary>
	/// <param name="shapeCharacters">The game's shape character table.</param>
	/// <exception cref="InvalidDataException">A numeric key has a value that is not an integer, or the shape grid is invalid.</exception>
	public static SamInfo? Parse( string text, ShapeCharacters shapeCharacters )
	{
		var values = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
		foreach ( Match match in Key.Matches( text ) )
			values.TryAdd( match.Groups[1].Value, match.Groups[2].Value.Trim() );
		if ( !values.ContainsKey( "Id" ) ) return null;
		int Number( string key )
		{
			if ( !values.TryGetValue( key, out var value ) ) return 0;
			var token = value.Split( (char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries ).FirstOrDefault() ?? "";
			return int.TryParse( token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number ) ? number
				: throw new InvalidDataException( $"Info.{key} is not an integer: '{value}'" );
		}
		string name = values.TryGetValue( "Name", out var quoted ) && quoted.IndexOf( '"' ) is int open and >= 0 && quoted.IndexOf( '"', open + 1 ) is int close and > 0
			? quoted[(open + 1)..close] : "";
		var shape = ShapeBlock.Match( text ) is { Success: true } block
			? new FootprintShape( block.Groups[1].Value.Replace( "\r", "" ).Split( '\n' ), shapeCharacters ) : null;
		return new SamInfo( Number( "Id" ), name, shape, Number( "EngineMapOffsetOverrideX" ), Number( "EngineMapOffsetOverrideY" ),
			Number( "EngineFootprintWidthOverride" ), Number( "EngineFootprintHeightOverride" ), Number( "DontApplyOffset" ) != 0 );
	}
}

/// <summary>An object type a save refers to by <see cref="SamInfo.Id"/>: where its model is and how it is placed.</summary>
/// <param name="Source">Host-defined location of the declaring archive (used to find the model and its textures).</param>
/// <param name="Width">Footprint cells along x at 0 degrees.</param>
/// <param name="Depth">Footprint cells along z at 0 degrees.</param>
public sealed record ObjectType( SamInfo Info, string Source, string Model, int Width, int Depth )
{
	public int Id => Info.Id;
	/// <summary>Fixed items (<c>DontApplyOffset</c>) are placed at their engine map offset, not at the saved cell (see <see cref="ObjectPlacement"/>).</summary>
	public bool IsFixed => Info.DontApplyOffset;
}

/// <summary>
/// Object types by id. ObjectTypes_FindById (US 0x412e90 / TPI 0x43c700) searches the registered definitions in order, so the
/// first registration of an id wins; later duplicates are kept out (<see cref="Duplicates"/>) rather than replacing it.
/// </summary>
/// <remarks>
/// Which archives are registered and in which order is the host's scan. The game appends definitions as
/// ObjectTypes_ScanSources (US 0x413930) enumerates its sources through the virtual file system (ObjectTypes_Add,
/// US 0x413c10); that enumeration order has not been traced. It cannot change a result for the retail data: no level of
/// either game declares an id twice.
/// </remarks>
public sealed class ObjectCatalog
{
	private readonly Dictionary<int, ObjectType> types = new();
	private readonly List<ObjectType> duplicates = [];

	public IReadOnlyCollection<ObjectType> Types => types.Values;
	/// <summary>Definitions whose id was already registered, in registration order.</summary>
	public IReadOnlyList<ObjectType> Duplicates => duplicates;

	/// <summary>Registers <paramref name="type"/>; returns false and records it as a duplicate when its id is already known.</summary>
	public bool Add( ObjectType type )
	{
		if ( types.TryAdd( type.Id, type ) ) return true;
		duplicates.Add( type );
		return false;
	}

	public ObjectType? Find( int id ) => types.GetValueOrDefault( id );
}
