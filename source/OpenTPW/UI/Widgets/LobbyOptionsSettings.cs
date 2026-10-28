namespace OpenTPW;

/// <summary>Global US options (0x4242c0), separate from the 39-byte player preferences (0x423e90).</summary>
internal sealed class LobbyOptionsSettings
{
	private static readonly string[] Presets = { "low", "med", "high" };
	public int Resolution { get; set; } = -1;
	public int Graphics { get; set; } = 1;
	public int AudioQuality { get; set; } = 100;
	public string Language { get; set; } = "";
	public string LanguagePack { get; set; } = "";
	public byte[]? Preferences { get; set; }
	public static LobbyOptionsSettings Current { get; private set; } = new();
	internal LobbyOptionsSettings Copy() => (LobbyOptionsSettings)MemberwiseClone();
	/// <summary>A pack with several folders remains selectable; keep a valid choice or show an explicit default.</summary>
	internal static string ChooseLanguage( string[] available, string previous )
	{
		if ( available.Length == 0 ) throw new InvalidDataException( "Selected pack and base contain no language folders" );
		string? Find( string value ) => available.FirstOrDefault( candidate => candidate.Equals( value, StringComparison.OrdinalIgnoreCase ) );
		return Find( previous ) ?? Find( "American" ) ?? Find( "English" ) ?? available[0];
	}
	internal void Validate()
	{
		if ( Resolution < -1 || Resolution > 2 || Graphics < 0 || Graphics > 2 || AudioQuality < 0 || AudioQuality > 100 )
			throw new InvalidDataException( "Global options outside the US control ranges" );
		if ( Language == null || LanguagePack == null ) throw new InvalidDataException( "Missing language settings" );
		if ( Preferences != null )
		{
			if ( Preferences.Length != 39 ) throw new InvalidDataException( "Invalid default player preferences" );
			var profile = PlayerProfileFile.Create( 0, Preferences );
			foreach ( var channel in Enum.GetValues<PlayerProfileFile.SoundChannel>() ) _ = profile.SoundVolume( channel );
		}
		if ( LanguagePack.Length != 0 ) _ = GameLanguage.PackDataFolder( LanguagePack );
	}
	/// <summary>Read the single executable-local settings file before language, audio and window construction.</summary>
	internal static void Initialize( Settings? configuration = null )
	{
		var settings = configuration ?? Settings.Default;
		Current = new()
		{
			Language = settings.Language.Trim(), LanguagePack = settings.LanguagePack.Trim(),
			Graphics = Array.IndexOf( Presets, settings.LobbyQuality.ToLowerInvariant() ),
			AudioQuality = settings.AudioQuality ?? DefaultAudioQuality(),
			Preferences = settings.LobbyPreferences == null ? null : (byte[])settings.LobbyPreferences.Clone()
		};
		for ( int i = 0; i < 3; i++ ) if ( settings.GameWindowSize == Size( i ) ) Current.Resolution = i;
		Current.Validate(); WriteTo( settings, Current );
	}
	private static int DefaultAudioQuality()
	{
		if ( !int.TryParse( new SettingsFile( "sound.sam" )["SoundInfo.DEFAULTQUALITY"], out int quality ) || quality is < 0 or > 100 )
			throw new InvalidDataException( "Invalid default sound quality" );
		return quality;
	}
	internal static System.Drawing.Point Size( int index ) => index switch
	{
		0 => new( 512, 384 ), 1 => new( 640, 480 ), 2 => new( 800, 600 ),
		_ => throw new ArgumentOutOfRangeException( nameof(index) )
	};
	private static void WriteTo( Settings settings, LobbyOptionsSettings draft )
	{
		settings.Language = draft.Language; settings.LanguagePack = draft.LanguagePack;
		settings.LobbyQuality = Presets[draft.Graphics]; settings.AudioQuality = draft.AudioQuality;
		settings.LobbyPreferences = draft.Preferences == null ? null : (byte[])draft.Preferences.Clone();
		if ( draft.Resolution >= 0 ) settings.GameWindowSize = Size( draft.Resolution );
	}
	/// <summary>
	/// Called on a worker after OK. Re-read both settings and profile to preserve unrelated fields;
	/// stage both files before publishing, and restore the first if the second publication fails.
	/// </summary>
	internal static void Save( string saveRoot, LobbyOptionsSettings draft, string? profilePath, byte[] preferences, string? settingsPath = null )
	{
		if ( preferences.Length != 39 ) throw new InvalidDataException( "Invalid player options" );
		draft = draft.Copy(); draft.Preferences = (byte[])preferences.Clone(); draft.Validate();
		string root = Path.GetFullPath( saveRoot );
		if ( profilePath != null && !Path.GetFullPath( profilePath ).StartsWith(
			Path.Combine( root, "users" ) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase ) )
			throw new InvalidDataException( "Profile is outside the save users directory" );
		Directory.CreateDirectory( root );
		string global = Path.GetFullPath( settingsPath ?? Settings.Default.FilePath );
		var settings = Settings.Load( global ); WriteTo( settings, draft );
		byte[]? previous = File.Exists( global ) ? File.ReadAllBytes( global ) : null;
		string staging = global + "." + Guid.NewGuid().ToString( "N" ) + ".tmp";
		string? profileStage = profilePath == null ? null : profilePath + "." + Guid.NewGuid().ToString( "N" ) + ".tmp";
		bool published = false;
		try
		{
			if ( profilePath != null )
			{
				var data = PlayerProfileFile.Read( File.ReadAllBytes( profilePath ) );
				preferences.CopyTo( data.Preferences, 0 );
				File.WriteAllBytes( profileStage!, data.Write() );
			}
			settings.Save(); published = true;
			if ( profileStage != null ) File.Move( profileStage, profilePath!, true );
		}
		catch
		{
			if ( published )
			{
				if ( previous == null ) File.Delete( global );
				else { File.WriteAllBytes( staging, previous ); File.Move( staging, global, true ); }
			}
			throw;
		}
		finally
		{
			if ( File.Exists( staging ) ) File.Delete( staging );
			if ( profileStage != null && File.Exists( profileStage ) ) File.Delete( profileStage );
		}
	}
	internal static void Accept( LobbyOptionsSettings draft ) { Current = draft.Copy(); WriteTo( Settings.Default, draft ); }
}
