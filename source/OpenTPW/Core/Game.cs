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
		// Check if the game data directory exists
		//
		if ( !Path.Exists( $"{Settings.Default.GamePath}/data/" ) )
		{
			throw new DirectoryNotFoundException( "Theme Park World not found" );
		}
		// Register game data directory
		FileSystem = new BaseFileSystem( $"{Settings.Default.GamePath}/data/" );
		FileSystem.RegisterArchiveHandler<WadArchive>( ".wad" );
		FileSystem.RegisterArchiveHandler<SdtArchive>( ".sdt" );
		//
		// Check if the save data directory exists (create if not)
		//
		if ( !Path.Exists( $"{Settings.Default.GamePath}/save/" ) )
		{
			Directory.CreateDirectory( $"{Settings.Default.GamePath}/save/" );
		}
		// Register save data directory
		SaveFileSystem = new BaseFileSystem( $"{Settings.Default.GamePath}/save/" );
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
		LobbyScene level = null!;
		LoadingScreen.Visible = true;
		_ = Task.Run( async () => {
			// Load ui.wad
			await LoadUIFiles();
			LoadingScreen.Progress = 0.0f;
			// Load scene
			level = new LobbyScene();
			LoadingScreen.Progress = 1.0f;
			LoadingScreen.Visible = false;
		} );
		//
		// Run game loop
		//
		using var music = new MusicPlayer( FileSystem );
		Render.OnUpdate += () => {
			level?.Update();
			if ( level != null )
			{
				music.Update( level.ActivePark?.Theme ?? (level.SelectedPlayer == null ? null : ParkThemes.Get( level.SelectedIsland ).DirectoryName),
					park: level.ActivePark != null, loading: level.IsLoadingPark );
			}
		};
		Render.OnRender += () => {
			level?.Render();
		};
		Render.OnRenderUI += () => {
			level?.RenderUI();
			LoadingScreen.RenderUI();
		};
		Render.Run();
	}

	private static async Task LoadUIFiles()
	{
		string[] GetFilePaths( string dir, bool loadSubDirectories = false )
		{
			var files = FileSystem.GetFiles( dir );
			if ( !loadSubDirectories )
			{
				return files;
			}

			var directories = FileSystem.GetDirectories( dir );
			foreach ( var directory in directories )
			{
				files = [.. files, .. GetFilePaths( dir, loadSubDirectories )];
			}

			return files;
		}

		var uiPath = "/ui";
		string[] files = GetFilePaths( uiPath );
		for ( int fileIdx = 0; fileIdx < files.Length; fileIdx++ )
		{
			string? path = files[fileIdx];
			if ( path.EndsWith( ".wct", StringComparison.OrdinalIgnoreCase ) )
			{
				_ = Resource.Get<Texture>( path );
			}

			if ( path.EndsWith( ".md2", StringComparison.OrdinalIgnoreCase ) )
			{
				_ = Resource.Get<Model>( path );
			}

			await Task.Delay( 1 );
			float progress = fileIdx / (float)files.Length;
			LoadingScreen.Progress = progress * 0.3f;
		}
	}
}
