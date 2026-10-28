using System.Globalization;

namespace OpenTPW;

/// <summary>The lobby subset of a retail quality preset.</summary>
public sealed class LobbyQuality
{
	private static BaseFileSystem? source;
	private static Dictionary<string, LobbyQuality>? presets;
	private static LobbyQuality? active;
	public int ParticleDensity { get; private set; }
	public string Preset { get; private set; }
	public int ObjectsPercent { get; private set; }
	public int TextureQuality { get; private set; }
	public int SkyLayers { get; private set; }
	public bool WeatherEnabled { get; private set; }
	/// <summary>GraphicalOptions.LIGHTING; 0 makes 0x44ce66 set 0x8c52c0 bits 0x1800, and 0x570ca0 then leaves vertices white.</summary>
	public bool LightingEnabled { get; private set; }
	public bool UseSmallTextures => TextureQuality <= 1;

	private LobbyQuality( string preset, SettingsFile settings )
	{
		Preset = preset;
		ParticleDensity = Read( settings, "GameOptions.PARTICLEDENSITY", 1, 4096 );
		ObjectsPercent = Read( settings, "GameOptions.LOBBYOBJECTS", 0, 100 );
		TextureQuality = Read( settings, "GraphicalOptions.TEXTUREQUALITY", 0, 3 );
		SkyLayers = Read( settings, "GraphicalOptions.SKYQUALITY", 0, 4 );
		if ( SkyLayers == 3 ) throw new InvalidDataException( "SKYQUALITY must be 0, 1, 2 or 4." );
		WeatherEnabled = Read( settings, "GameOptions.WEATHER", 0, 1 ) != 0;
		LightingEnabled = Read( settings, "GraphicalOptions.LIGHTING", 0, 1 ) != 0;
	}

	/// <summary>Reads the selected preset; the retail initial detail level is medium (0x423170).</summary>
	public static LobbyQuality Load( string? preset = null )
	{
		if ( source != FileSystem || presets == null )
		{
			source = FileSystem;
			presets = new( StringComparer.OrdinalIgnoreCase );
			foreach ( string name in new[] { "low", "med", "high" } )
				presets.Add( name, new LobbyQuality( name, new SettingsFile( $"/{name}.sam" ) ) );
			active = null;
		}
		string nameKey = (preset ?? Settings.Default.LobbyQuality).ToLowerInvariant();
		if ( !presets.TryGetValue( nameKey, out var value ) ) throw new ArgumentException( $"Unknown lobby quality preset: {nameKey}", nameof(preset) );
		if ( preset != null ) return value;
		return active ??= (LobbyQuality)value.MemberwiseClone();
	}
	/// <summary>Switch only preloaded preset values; scene consumers retain the same live object.</summary>
	internal static void Apply( string preset )
	{
		var current = Load(); var value = Load( preset );
		current.Preset = value.Preset; current.ObjectsPercent = value.ObjectsPercent;
		current.TextureQuality = value.TextureQuality; current.SkyLayers = value.SkyLayers;
		current.WeatherEnabled = value.WeatherEnabled; current.LightingEnabled = value.LightingEnabled;
		current.ParticleDensity = value.ParticleDensity;
		Settings.Default.LobbyQuality = preset;
	}

	private static int Read( SettingsFile settings, string key, int minimum, int maximum )
	{
		if ( !int.TryParse( settings[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value ) || value < minimum || value > maximum )
			throw new InvalidDataException( $"Quality setting {key} must be an integer from {minimum} to {maximum}." );
		return value;
	}

	public string TextureDirectory( string directory )
	{
		// 0x54a300 -> 0x467430 switches the directory, not individual missing textures.
		directory = directory.Replace( '\\', '/' ).TrimEnd( '/' );
		if ( !UseSmallTextures ) return directory;
		int slash = directory.LastIndexOf( '/' );
		return directory[(slash + 1)..].Equals( "textures", StringComparison.OrdinalIgnoreCase )
			? directory[..(slash + 1)] + "stexture" : directory;
	}
}
