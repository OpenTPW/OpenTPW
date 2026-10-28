using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTPW;

/// <summary>
/// OpenTPW settings, kept in <see cref="FileName"/> beside the executable. Retail reads its install folder,
/// language and options from the registry (HKLM\Software\Bullfrog Productions Ltd\Theme Park World);
/// OpenTPW reads neither the registry nor per-user application data. A missing file means defaults;
/// <see cref="Save"/> creates it.
/// </summary>
internal sealed class Settings
{
	public const string FileName = "OpenTPW.settings.json";

	private static readonly JsonSerializerOptions json = new()
	{
		WriteIndented = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
	};

	public static Settings Default { get; } = Load( Path.Combine( AppContext.BaseDirectory, FileName ) );

	/// <summary>Theme Park World folder; empty searches from the current and executable folders (<see cref="GameDir"/>).</summary>
	public string GamePath { get; set; } = "";

	/// <summary>Language pack folder searched before the game's data (<see cref="GameLanguage.PackDataFolder"/>); empty for none.</summary>
	public string LanguagePack { get; set; } = "";

	/// <summary>Folder under data/Language; empty chooses one (<see cref="GameLanguage.Resolve"/>).</summary>
	public string Language { get; set; } = "";

	[JsonConverter( typeof( PointConverter ) )]
	public Point GameWindowSize { get; set; } = new( 1280, 720 );

	public string PreferredBackend { get; set; } = "Vulkan";

	public string LobbyQuality { get; set; } = "med";

	/// <summary>US sound quality percentage; null uses SoundInfo.DEFAULTQUALITY from sound.sam.</summary>
	public int? AudioQuality { get; set; }

	/// <summary>The 39-byte default preferences for the lobby and newly created players, without progress fields.</summary>
	public byte[]? LobbyPreferences { get; set; }

	[JsonIgnore]
	public string FilePath { get; private set; } = "";

	public static Settings Load( string path )
	{
		var full = Path.GetFullPath( path );
		if ( !File.Exists( full ) ) return new Settings { FilePath = full };
		Settings settings;
		try
		{
			settings = JsonSerializer.Deserialize<Settings>( File.ReadAllBytes( full ), json )
				?? throw new InvalidDataException( $"Settings file is empty: {full}" );
		}
		catch ( JsonException e )
		{
			throw new InvalidDataException( $"Invalid settings file {full}: {e.Message}", e );
		}
		if ( settings.GamePath == null || settings.LanguagePack == null || settings.Language == null
			|| settings.PreferredBackend == null || settings.LobbyQuality == null )
			throw new InvalidDataException( $"Settings file has a null value: {full}" );
		settings.FilePath = full;
		return settings;
	}

	/// <summary>Writes a temporary file and replaces the settings file with it, so a failed write keeps the old one.</summary>
	public void Save()
	{
		var temporary = FilePath + "." + Guid.NewGuid().ToString( "N" ) + ".tmp";
		try
		{
			File.WriteAllBytes( temporary, JsonSerializer.SerializeToUtf8Bytes( this, json ) );
			File.Move( temporary, FilePath, true );
		}
		finally { if ( File.Exists( temporary ) ) File.Delete( temporary ); }
	}

	/// <summary>Writes a point as [x, y].</summary>
	private sealed class PointConverter : JsonConverter<Point>
	{
		public override Point Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
		{
			var values = JsonSerializer.Deserialize<int[]>( ref reader, options );
			return values is { Length: 2 } ? new Point( values[0], values[1] ) : throw new JsonException( "Expected [x, y]" );
		}

		public override void Write( Utf8JsonWriter writer, Point value, JsonSerializerOptions options )
		{
			writer.WriteStartArray();
			writer.WriteNumberValue( value.X );
			writer.WriteNumberValue( value.Y );
			writer.WriteEndArray();
		}
	}
}
