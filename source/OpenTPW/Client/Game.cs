namespace OpenTPW;

/// <summary>
/// Handles the creation and management of various systems, including the game
/// window.
/// </summary>
internal static class Game
{
	public static void Run( string[] args )
	{
		Log = new();

		//
		// Find the game data directory (throws if there is none)
		//
		var gamePath = GameDir.GamePath;
		Log.Info( $"Game data: {gamePath}" );
		// Shell launchers can supply their own working directory. Resolve engine assets once
		// before shaders, icons and the editor cache use relative paths.
		Directory.SetCurrentDirectory( EngineContent.Find( Environment.CurrentDirectory, AppContext.BaseDirectory ) );

		// Register game data directory
		FileSystem = new BaseFileSystem( $"{gamePath}/data/" );
		FileSystem.RegisterArchiveHandler<WadArchive>( ".wad" );
		FileSystem.RegisterArchiveHandler<SdtArchive>( ".sdt" );
		LobbyOptionsSettings.Initialize();
		// A language pack's data is searched before the game's, as if copied over it (retail install step).
		string[]? packLanguages = null;
		if ( !string.IsNullOrWhiteSpace( Settings.Default.LanguagePack ) )
		{
			var packData = GameLanguage.PackDataFolder( Settings.Default.LanguagePack );
			FileSystem.AddOverlay( packData );
			packLanguages = GameLanguage.PackLanguages( packData );
			Log.Info( $"Language pack: {packData}" );
		}
		// Before any string table or font is read: they all come from this folder.
		GameLanguage.Select( GameLanguage.Resolve( GameLanguage.Available( FileSystem ), Settings.Default.Language, packLanguages ) );
		Log.Info( $"Language: {GameLanguage.Name}" );

		//
		// Check if the save data directory exists (create if not)
		//
		if ( !Path.Exists( $"{gamePath}/save/" ) )
			Directory.CreateDirectory( $"{gamePath}/save/" );

		// Register save data directory
		SaveFileSystem = new BaseFileSystem( $"{gamePath}/save/" );

		//
		// Custom OpenTPW cache directory (mainly for editor-related stuff)
		//
		CacheFileSystem = new BaseFileSystem( $"./.opentpw" );

		//
		// Init renderer
		//
		Render = new();

		//
		// Create level
		//
#if DEBUG
		// Debug options: --verify-lobby <dir> runs scene transitions through production callbacks
		// without injecting OS input; --lobby-view <file> pins the camera for retail comparison;
		// --capture-frames <dir> saves 30 consecutive frames after a 20 second warm-up.
		var options = DebugOptions( args );
		LobbyCameraMode.FixedViewPath = options.GetValueOrDefault( "--lobby-view" );
#endif
		using var audio = new AudioSystem( unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()) );
		audio.ConfigureQuality( LobbyOptionsSettings.Current.AudioQuality );
		if ( LobbyOptionsSettings.Current.Preferences is { } preferences ) audio.ConfigurePreferences( preferences );
		if ( !audio.Available ) Log.Warning( $"Audio output unavailable: {audio.Failure}" );
		var level = new Level( "jungle", audio );
#if DEBUG
		LobbyAcceptance? acceptance = options.TryGetValue( "--verify-lobby", out var output ) ? new( level, output ) : null;
		LobbyOptionsAcceptance? optionsAcceptance = options.TryGetValue( "--verify-options", out var optionsOutput ) ? new( level, optionsOutput ) : null;
		FrameCapture? frames = options.TryGetValue( "--capture-frames", out var framesOutput ) ? new( framesOutput ) : null;
#endif

		//
		// Run game loop
		//
		Render.OnUpdate += level.Update;
#if DEBUG
		if ( acceptance != null ) Render.OnUpdate += acceptance.Update;
		if ( optionsAcceptance != null ) Render.OnUpdate += optionsAcceptance.Update;
		if ( frames != null ) Render.OnUpdate += frames.Update;
#endif
		Render.OnRender += level.Render;
		Render.Run();
		Log.Info( "Render loop stopped" );
		// A closed SDL window does not stop the audio callback. Drain it before reporting completion.
		audio.Dispose();
		Log.Info( "Audio output disposed" );
#if DEBUG
		acceptance?.VerifyCompleted();
#endif
	}

#if DEBUG
	private static Dictionary<string, string> DebugOptions( string[] args )
	{
		var options = new Dictionary<string, string>();
		for ( int i = 0; i < args.Length; i += 2 )
		{
			if ( args[i] is not ("--verify-lobby" or "--lobby-view" or "--capture-frames" or "--verify-options") || i + 1 >= args.Length || !options.TryAdd( args[i], args[i + 1] ) )
				throw new ArgumentException( $"Expected --verify-lobby <dir>, --verify-options <dir>, --lobby-view <file>, --capture-frames <dir>, got: {string.Join( ' ', args )}" );
		}
		return options;
	}
#endif
}
