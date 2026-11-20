using System.Runtime.InteropServices;
using SDL;
using Veldrid;
using static SDL.SDL3;

namespace OpenTPW;

/// <summary>SDL3 application window and event pump; renderer ownership stays in the game project.</summary>
public sealed unsafe class Window : IDisposable
{
	public static Window Current { get; private set; } = null!;
	internal SDL_Window* Handle { get; private set; }
	private readonly SdlInputSnapshot snapshot = new();
	public bool Exists { get; private set; } = true;
	public Point2 Size
	{
		get
		{
			int width, height;
			Check( SDL_GetWindowSize( Handle, &width, &height ) );
			return new Point2( width, height );
		}
	}
	public Action<Point2>? OnResized { get; set; }
	public bool Visible
	{
		get => (SDL_GetWindowFlags( Handle ) & SDL_WindowFlags.SDL_WINDOW_HIDDEN) == 0;
		set => Check( value ? SDL_ShowWindow( Handle ) : SDL_HideWindow( Handle ) );
	}

	public Window( int width, int height, string title, bool startHidden = false )
	{
		Check( SDL_InitSubSystem( SDL_InitFlags.SDL_INIT_VIDEO ) );
		Handle = SDL_CreateWindow( title, width, height, SDL_WindowFlags.SDL_WINDOW_RESIZABLE |
			(startHidden ? SDL_WindowFlags.SDL_WINDOW_HIDDEN : 0) );
		if ( Handle == null )
		{
			SDL_QuitSubSystem( SDL_InitFlags.SDL_INIT_VIDEO );
			throw new InvalidOperationException( SDL_GetError() );
		}
		try
		{
			Check( SDL_SetWindowPosition( Handle, 32, 32 ) );
			Check( SDL_StartTextInput( Handle ) );
			Screen.UpdateFrom( Size );
			Current = this;
		}
		catch { Dispose(); throw; }
	}

	/// <summary>Veldrid creates its Vulkan surface from SDL3's native Windows properties.</summary>
	public SwapchainSource CreateSwapchainSource()
	{
		if ( !OperatingSystem.IsWindows() ) throw new PlatformNotSupportedException( "The current renderer requires Win32 window handles." );
		var properties = SDL_GetWindowProperties( Handle );
		var hwnd = SDL_GetPointerProperty( properties, SDL_PROP_WINDOW_WIN32_HWND_POINTER, IntPtr.Zero );
		var instance = SDL_GetPointerProperty( properties, SDL_PROP_WINDOW_WIN32_INSTANCE_POINTER, IntPtr.Zero );
		if ( hwnd == IntPtr.Zero || instance == IntPtr.Zero ) throw new InvalidOperationException( $"SDL3 window handles unavailable: {SDL_GetError()}" );
		return SwapchainSource.CreateWin32( hwnd, instance );
	}

	public InputSnapshot PumpEvents()
	{
		snapshot.BeginFrame();
		bool resized = false;
		SDL_Event ev;
		while ( SDL_PollEvent( &ev ) )
		{
			var type = (SDL_EventType)ev.type;
			if ( type == SDL_EventType.SDL_EVENT_QUIT ) Close();
			// All event structures handled here carry windowID at the same offset.
			if ( ev.window.windowID != SDL_GetWindowID( Handle ) ) continue;
			if ( type == SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED ) Close();
			else if ( type == SDL_EventType.SDL_EVENT_WINDOW_RESIZED ) resized = true;
			else snapshot.Process( ev );
		}
		float x, y;
		SDL_GetMouseState( &x, &y );
		snapshot.MousePosition = new System.Numerics.Vector2( x, y );
		if ( resized && Exists )
		{
			var size = Size;
			if ( size.X > 0 && size.Y > 0 )
			{
				Screen.UpdateFrom( size );
				OnResized?.Invoke( size );
			}
		}
		return snapshot;
	}

	// Keep the native surface alive until the renderer has finished its final submission.
	public void Close() => Exists = false;
	public void Dispose()
	{
		if ( Handle == null ) return;
		Exists = false;
		SDL_DestroyWindow( Handle );
		Handle = null;
		SDL_QuitSubSystem( SDL_InitFlags.SDL_INIT_VIDEO );
		if ( ReferenceEquals( Current, this ) ) Current = null!;
	}

	internal static void Check( SDLBool success )
	{
		if ( !success ) throw new InvalidOperationException( SDL_GetError() );
	}
}
