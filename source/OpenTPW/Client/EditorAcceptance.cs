#if DEBUG
using System.Text.Json;
using OpenTPW.ModKit;
using SDL;
using static SDL.SDL3;

namespace OpenTPW;

/// <summary>Opt-in editor acceptance: toggles ModKit with '~', browses data, previews WCT/MP2 and plays audio.</summary>
internal sealed unsafe class EditorAcceptance
{
	private readonly Renderer render;
	private readonly string output;
	private readonly List<string> checks = new();
	private readonly double beginning = Time.Now;
	private double changed = Time.Now;
	private int stage;
	private int framesInStage;
	private string? lastPlayedPath;
	private long lastPlayedHandle;
	private string? textureFile;
	private string? soundFile;
	private bool previewWrapped;

	internal EditorAcceptance( Renderer render, string output )
	{
		this.render = render;
		this.output = Path.GetFullPath( output );
		Directory.CreateDirectory( this.output );
	}

	private void EnsurePreviewWrapped()
	{
		if ( previewWrapped ) return;
		previewWrapped = true;
		var upstreamPreview = render.PreviewSound;
		render.PreviewSound = path =>
		{
			lastPlayedPath = path;
			lastPlayedHandle = upstreamPreview?.Invoke( path ) ?? 0;
			Log.Info( $"EditorAcceptance played sound '{path}' -> handle {lastPlayedHandle}" );
			return lastPlayedHandle;
		};
	}

	private void Next( int value )
	{
		stage = value;
		changed = Time.Now;
		framesInStage = 0;
	}

	private void Check( bool value, string description )
	{
		if ( !value ) throw new InvalidOperationException( $"Editor acceptance failed: {description}" );
		checks.Add( description );
		Log.Info( $"EditorAcceptance: {description}" );
	}

	internal void Update()
	{
		if ( Time.Now - beginning > 90 ) throw new TimeoutException( $"Editor acceptance timed out at stage {stage}" );
		double elapsed = Time.Now - changed;
		framesInStage++;

		switch ( stage )
		{
			case 0 when elapsed > 1.0 && framesInStage >= 10:
				EnsurePreviewWrapped();
				PushGraveKey( down: true );
				Next( 1 );
				break;

			case 1:
				PushGraveKey( down: false );
				Check( render.EditorVisible, "pressing '~' toggles ModKit editor visibility" );
				var browser = EditorInstance.FindTab<FileBrowser>()
					?? throw new InvalidOperationException( "FileBrowser tab was not registered" );
				browser.visible = true;
				Next( 2 );
				break;

			case 2 when framesInStage >= 4 && elapsed > 0.2:
				Check( EditorInstance.FindTab<FileBrowser>() is { visible: true }, "File Browser panel is open" );
				Capture( "editor-file-browser" );
				var wctBrowser = EditorInstance.FindTab<FileBrowser>()!;
				Navigate( wctBrowser, "generic", "dynamic", "stexture" );
				var wctFile = FindFile( wctBrowser, "blue.wct" ) ?? FindFileByExtension( wctBrowser, ".wct" );
				textureFile = wctFile.Name;
				wctBrowser.SelectFile( wctFile );
				Next( 3 );
				break;

			case 3 when framesInStage >= 4 && !FileBrowser.ThumbnailsBusy && elapsed > 0.4:
				var texBrowser = EditorInstance.FindTab<FileBrowser>()!;
				Check( texBrowser.CurrentFileViewer is TextureViewer, $"TextureViewer opened '{textureFile}'" );
				Check( EditorInstance.Renderer.TextureCount >= 2, "TextureViewer uploaded GPU texture" );
				Capture( "editor-texture-viewer" );
				texBrowser.BasePath = "";
				Navigate( texBrowser, "global", "sound", "UIHD" );
				var mp2File = FindFileByExtension( texBrowser, ".mp2" );
				soundFile = mp2File.Name;
				texBrowser.SelectFile( mp2File );
				Next( 4 );
				break;

			case 4 when framesInStage >= 3 && elapsed > 0.25:
				var sndBrowser = EditorInstance.FindTab<FileBrowser>()!;
				Check( sndBrowser.CurrentFileViewer is SoundViewer, $"SoundViewer opened '{soundFile}'" );
				var viewer = (SoundViewer)sndBrowser.CurrentFileViewer;
				viewer.PlaySound();
				Check( lastPlayedHandle > 0 && lastPlayedPath == soundFile, $"SoundViewer played '{soundFile}' (handle {lastPlayedHandle})" );
				Next( 5 );
				break;

			case 5 when framesInStage >= 3 && elapsed > 0.25:
				Capture( "editor-sound-viewer" );
				Next( 6 );
				Window.Current.Close();
				break;
		}
	}

	internal void VerifyCompleted()
	{
		Check( stage == 6 && !Window.Current.Exists, "editor acceptance reached final stage before exit" );
		File.WriteAllText( Path.Join( output, "editor-acceptance.json" ), JsonSerializer.Serialize( new
		{
			passed = true,
			width = render.ResolveColorTexture.Width,
			height = render.ResolveColorTexture.Height,
			textureFile,
			soundFile,
			lastPlayedPath,
			lastPlayedHandle,
			checks
		}, new JsonSerializerOptions { WriteIndented = true } ) );
		Log.Info( "Editor acceptance PASS" );
	}

	private Editor EditorInstance => render.EditorHost?.Editor
		?? throw new InvalidOperationException( "EditorHost was not initialized" );

	private void PushGraveKey( bool down )
	{
		var ev = new SDL_Event
		{
			type = (uint)(down ? SDL_EventType.SDL_EVENT_KEY_DOWN : SDL_EventType.SDL_EVENT_KEY_UP),
			key = new SDL_KeyboardEvent
			{
				type = down ? SDL_EventType.SDL_EVENT_KEY_DOWN : SDL_EventType.SDL_EVENT_KEY_UP,
				windowID = SDL_GetWindowID( render.Window.Handle ),
				scancode = SDL_Scancode.SDL_SCANCODE_GRAVE,
				key = SDL_Keycode.SDLK_GRAVE,
				down = down
			}
		};
		if ( !SDL_PushEvent( &ev ) )
			throw new InvalidOperationException( $"SDL_PushEvent failed: {SDL_GetError()}" );
	}

	private static void Navigate( FileBrowser browser, params string[] segments )
	{
		foreach ( var seg in segments )
		{
			var next = browser.CurrentDirectory.Children
				.OfType<FileBrowser.FileSystemDirectory>()
				.FirstOrDefault( d => LeafName( d.Name ).Equals( seg, StringComparison.OrdinalIgnoreCase ) )
				?? throw new DirectoryNotFoundException( $"Directory '{seg}' not found under '{browser.BasePath}'" );
			browser.BasePath = next.Name;
		}
	}

	private static FileBrowser.FileSystemFile? FindFile( FileBrowser browser, string leafName ) =>
		browser.CurrentDirectory.Children
			.OfType<FileBrowser.FileSystemFile>()
			.FirstOrDefault( f => LeafName( f.Name ).Equals( leafName, StringComparison.OrdinalIgnoreCase ) );

	private static FileBrowser.FileSystemFile FindFileByExtension( FileBrowser browser, string ext ) =>
		browser.CurrentDirectory.Children
			.OfType<FileBrowser.FileSystemFile>()
			.FirstOrDefault( f => Path.GetExtension( f.Name ).Equals( ext, StringComparison.OrdinalIgnoreCase ) )
		?? throw new FileNotFoundException( $"No '{ext}' file found under '{browser.BasePath}'" );

	private static string LeafName( string path ) =>
		Path.GetFileName( path.TrimEnd( '/', '\\' ) );

	private void Capture( string name ) =>
		FrameCapture.SaveBmp( Path.Join( output, name + ".bmp" ) );
}
#endif
