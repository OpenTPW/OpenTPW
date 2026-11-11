using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenTPW.Park;

/// <summary>
/// The <c>Info.*</c> keys of an object's <c>.sam</c> that placement uses. Both games share the keyword table
/// (US 0x744b6c / TPI 0x9a2cfc: type, name, value; Theme Park Inc adds <c>Info.Type</c> after <c>Info.Id</c>), so values
/// are looked up by key name and the extra key needs no parameter.
/// </summary>
/// <remarks>
/// ParkObject_CreateMesh (US 0x4dd500 / TPI 0x5df740) reads the definition at +0x18/+0x20/+0x24/+0x38 (TPI +0x1c/+0x24/+0x28/+0x3c,
/// one word later for the extra key). Those offsets are Shape, EngineMapOffsetOverrideX/Y and DontApplyOffset when the table's
/// entries are laid out four bytes each from +0x10; the generic loader that stores them (US 0x401030) is not fully traced.
/// </remarks>
public sealed record SamInfo( int Id, string Name, FootprintShape? Shape, int EngineMapOffsetOverrideX, int EngineMapOffsetOverrideZ,
	int EngineFootprintWidthOverride, int EngineFootprintHeightOverride, bool DontApplyOffset )
{
	// Keys are matched at a line start so commented-out or embedded mentions are not taken.
	private static readonly Regex Key = new( @"^Info\.(\w+)[ \t]*([^\r\n]*)", RegexOptions.Multiline | RegexOptions.CultureInvariant );
	private static readonly Regex ShapeBlock = new( @"^Info\.Shape[^\r\n]*\r?\n---[ \t]*\r?\n(.*?)\r?\n---", RegexOptions.Multiline | RegexOptions.Singleline );

	/// <summary>Reads the keys from <c>.sam</c> text; null when it declares no <c>Info.Id</c> (not an object definition).</summary>
	/// <exception cref="InvalidDataException">A numeric key has a value that is not an integer.</exception>
	public static SamInfo? Parse( string text )
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
			? new FootprintShape( block.Groups[1].Value.Replace( "\r", "" ).Split( '\n' ) ) : null;
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
/// <remarks>Which archives are registered and in which order is the host's scan; the game's registration order is not established.</remarks>
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
