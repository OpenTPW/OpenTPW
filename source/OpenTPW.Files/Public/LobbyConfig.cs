using System.Globalization;
using System.Text;
using N = System.Numerics;

namespace OpenTPW;

/// <summary>The function-style configuration stored in lobby.wad. Values retain retail units.</summary>
public sealed class LobbyConfig
{
	public float IslandFovDegrees { get; private set; } = 100;
	/// <summary>Spin rate in the original retail time units.</summary>
	public float SpinSpeed { get; private set; } = 0.02f;
	public float SpinRadius { get; private set; } = 70;
	public float VerticalOffset { get; private set; } = 20;
	public float GlobeRadiusOut { get; private set; } = 475;
	public float GlobeRadiusIn { get; private set; } = 200;
	/// <summary>Retail X/Z coordinates indexed by ISLAND's numeric ID, including unused camera slots.</summary>
	public IReadOnlyDictionary<int, N.Vector2> CameraPositions { get; private set; } = null!;
	public IReadOnlyList<LobbyIslandConfig> Islands { get; private set; } = null!;

	public static LobbyConfig Load( string directory = "lobby" )
	{
		// These are the four retail theme files; unrelated text resources are not island declarations.
		var themes = new[] { "jungle", "fantasy", "hallow", "space" };
		return Parse( FileSystem.ReadAllText( $"{directory}/lobby.txt" ), themes.Select(
			x => (x, FileSystem.ReadAllText( $"{directory}/{x}.txt" )) ) );
	}

	public static LobbyConfig Parse( string lobbyText, IEnumerable<(string ThemeName, string Text)> islandTexts )
	{
		var result = new LobbyConfig();
		var positions = new Dictionary<int, N.Vector2>();
		foreach ( var command in Commands( lobbyText ) )
		{
			var a = command.Arguments;
			if ( command.Name == "ISLANDCAMERAPOSITION" )
			{
				RequireCount( command, 3 );
				positions.Add( Integer( a[0] ), new( Number( a[1] ), Number( a[2] ) ) );
				continue;
			}
			RequireCount( command, 1 );
			var value = Number( a[0] );
			switch ( command.Name )
			{
				case "ISLANDFOV": result.IslandFovDegrees = value; break;
				case "SPINSPEED": result.SpinSpeed = value; break;
				case "SPINRADIUS": result.SpinRadius = value; break;
				case "VERTICALOFFSET": result.VerticalOffset = value; break;
				case "GLOBERADIUSOUT": result.GlobeRadiusOut = value; break;
				case "GLOBERADIUSIN": result.GlobeRadiusIn = value; break;
				default: throw new FormatException( $"Unknown lobby command {command.Name}" );
			}
		}
		var islands = islandTexts.Select( x => ParseIsland( x.ThemeName, x.Text ) ).OrderBy( x => x.Id ).ToArray();
		if ( islands.Select( x => x.Id ).Distinct().Count() != islands.Length )
			throw new FormatException( "Duplicate ISLAND ID" );
		foreach ( var island in islands )
			if ( !positions.ContainsKey( island.Id ) )
				throw new FormatException( $"No camera position for island {island.Id}" );
		result.CameraPositions = new System.Collections.ObjectModel.ReadOnlyDictionary<int, N.Vector2>( positions );
		result.Islands = Array.AsReadOnly( islands );
		return result;
	}

	private static LobbyIslandConfig ParseIsland( string themeName, string text )
	{
		LobbyIslandConfig? island = null;
		var flying = new List<LobbyFlyingMeshConfig>();
		var sky = N.Vector3.Zero;
		int lightning = 0;
		bool rainy = false;
		foreach ( var command in Commands( text ) )
		{
			var a = command.Arguments;
			switch ( command.Name )
			{
				case "ISLAND":
					RequireCount( command, 7 );
					if ( island != null ) throw new FormatException( $"Multiple ISLAND declarations in {themeName}" );
					island = new( Integer( a[0] ), themeName, ResourcePath( a[1] ), a[2], a[3], a[4], Number( a[5] ), Number( a[6] ) );
					break;
				case "FLYINGMESH":
					RequireCount( command, 7 );
					flying.Add( new( ResourcePath( a[0] ), a[1], Integer( a[2] ), new( Number( a[3] ), Number( a[4] ), Number( a[5] ) ), Number( a[6] ) ) );
					break;
				case "SKYCOLOUR":
					RequireCount( command, 3 );
					sky = new( Number( a[0] ), Number( a[1] ), Number( a[2] ) );
					break;
				case "LIGHTNING": RequireCount( command, 1 ); lightning = Integer( a[0] ); break;
				case "RAINY": RequireCount( command, 1 ); rainy = Integer( a[0] ) != 0; break;
				default: throw new FormatException( $"Unknown island command {command.Name}" );
			}
		}
		if ( island == null ) throw new FormatException( $"No ISLAND declaration in {themeName}" );
		return island with { FlyingMeshes = flying.AsReadOnly(), SkyColour = sky, Lightning = lightning, Rainy = rainy };
	}

	private static string ResourcePath( string path )
	{
		path = path.Replace( '\\', '/' ).Trim( '/' );
		return path.StartsWith( "data/", StringComparison.OrdinalIgnoreCase ) ? path[5..] : path;
	}

	private static float Number( string value )
	{
		if ( !float.TryParse( value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result ) || !float.IsFinite( result ) )
			throw new FormatException( $"Invalid lobby number '{value}'" );
		return result;
	}
	private static int Integer( string value ) => int.Parse( value, NumberStyles.Integer, CultureInfo.InvariantCulture );
	private static void RequireCount( Command command, int count )
	{
		if ( command.Arguments.Length != count )
			throw new FormatException( $"{command.Name} expects {count} arguments, got {command.Arguments.Length}" );
	}

	private sealed record Command( string Name, string[] Arguments );
	private static IEnumerable<Command> Commands( string text )
	{
		// SAMParser reads whitespace-delimited key/value pairs, whereas lobby files use quoted arguments.
		int p = 0;
		while ( p < text.Length )
		{
			if ( char.IsWhiteSpace( text[p] ) || text[p] == ';' ) { p++; continue; }
			if ( text[p] == '#' || (text[p] == '/' && p + 1 < text.Length && text[p + 1] == '/') )
			{
				while ( p < text.Length && text[p] != '\n' ) p++;
				continue;
			}
			int start = p;
			while ( p < text.Length && (char.IsLetterOrDigit( text[p] ) || text[p] == '_') ) p++;
			if ( start == p ) throw new FormatException( $"Expected lobby command at character {p}" );
			string name = text[start..p].ToUpperInvariant();
			while ( p < text.Length && char.IsWhiteSpace( text[p] ) ) p++;
			if ( p == text.Length || text[p++] != '(' ) throw new FormatException( $"Expected '(' after {name}" );
			var arguments = new List<string>();
			var value = new StringBuilder();
			bool quoted = false, closed = false;
			while ( p < text.Length )
			{
				char c = text[p++];
				if ( c == '"' ) { quoted = !quoted; continue; }
				if ( !quoted && (c == ',' || c == ')') )
				{
					arguments.Add( value.ToString().Trim() );
					value.Clear();
					if ( c == ')' ) { closed = true; break; }
				}
				else value.Append( c );
			}
			if ( !closed ) throw new FormatException( $"Unterminated {name}" );
			yield return new( name, arguments.ToArray() );
		}
	}
}

public sealed record LobbyIslandConfig( int Id, string ThemeName, string ResourceDirectory, string ModelName,
	string GateModelName, string Name, float RotationDegrees, float Height )
{
	// Height is the retail Y-up camera/flock anchor; the island and gate model roots stay at Y=0.
	public string ModelPath => $"{ResourceDirectory}/{ModelName}.md2";
	public string GateModelPath => $"{ResourceDirectory}/{GateModelName}.md2";
	/// <summary>RGB components in the original 0..255 range.</summary>
	public N.Vector3 SkyColour { get; init; }
	public int Lightning { get; init; }
	public bool Rainy { get; init; }
	public IReadOnlyList<LobbyFlyingMeshConfig> FlyingMeshes { get; init; } = Array.Empty<LobbyFlyingMeshConfig>();
}

public sealed record LobbyFlyingMeshConfig( string ResourceDirectory, string ModelName, int Count, N.Vector3 Extents, float Speed )
{
	public string ModelPath => $"{ResourceDirectory}/{ModelName}.md2";
	/// <summary>0x5d4e80: count × LOBBYOBJECTS percent / 100 with integer division, keeping at least one actor.</summary>
	public int CountAtQuality( int percent ) => Math.Max( 1, Count * Math.Clamp( percent, 0, 100 ) / 100 );
}
