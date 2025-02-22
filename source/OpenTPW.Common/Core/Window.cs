using System.Runtime.InteropServices;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Vulkan.Win32;

namespace OpenTPW;

/// <summary>
/// Contains code for the instantiation and management of a window, the game editor,
/// ImGUI, inputs, the renderer, and the world itself.
/// </summary>
public class Window
{
	public static Window Current { get; set; }
	public Sdl2Window SdlWindow { get; private set; }
	public Point2 Size => new Point2( SdlWindow.Width, SdlWindow.Height );

	public Action<Point2>? OnResized { get; set; }

	public bool Visible
	{
		get => SdlWindow.Visible;
		set => SdlWindow.Visible = value;
	}

	public Window( int width, int height, string title, bool startHidden = false )
	{
		Current ??= this;

		var windowCreateInfo = new WindowCreateInfo()
		{
			WindowWidth = width,
			WindowHeight = height,
			WindowTitle = title,
			X = 32,
			Y = 32,
			WindowInitialState = startHidden ? Veldrid.WindowState.Hidden : Veldrid.WindowState.Normal
		};

		SdlWindow = VeldridStartup.CreateWindow( windowCreateInfo );
		SdlWindow.Resizable = false;
		SdlWindow.Resized += SdlWindow_Resized;
		Screen.UpdateFrom( Size );
	}

	private void SdlWindow_Resized()
	{
		Screen.UpdateFrom( Size );
		OnResized?.Invoke( Size );
	}
}
