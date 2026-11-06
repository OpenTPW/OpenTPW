using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SDL;

namespace OpenTPW.Tests;

/// <summary>Exercises SDL3 event translation and the game's input state without OS input injection.</summary>
[TestClass]
[DoNotParallelize]
public unsafe class SdlInputTests
{
	private Window window = null!;

	[TestInitialize]
	public void CreateWindow()
	{
		window = new Window( 320, 240, "SDL3 input regression", true );
		Input.Keyboard = new(); Input.Mouse = new();
	}

	[TestCleanup]
	public void DestroyWindow() => window.Dispose();

	[TestMethod]
	public void NativeWindowPropertiesResizeAndCloseEventsWork()
	{
		Assert.IsTrue( window.Exists ); Assert.IsFalse( window.Visible );
		Assert.AreEqual( new Point2( 320, 240 ), window.Size );
		int width, height;
		Window.Check( SDL.SDL3.SDL_GetWindowSizeInPixels( window.Handle, &width, &height ) );
		Assert.AreEqual( window.Size, new Point2( width, height ) );
		Window.Check( SDL.SDL3.SDL_SetWindowSize( window.Handle, 400, 300 ) );
		window.PumpEvents(); Assert.AreEqual( new Point2( 400, 300 ), window.Size );
		var ev = new SDL_Event { window = new SDL_WindowEvent { type = SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED, windowID = SDL.SDL3.SDL_GetWindowID( window.Handle ) } };
		Window.Check( SDL.SDL3.SDL_PushEvent( &ev ) ); window.PumpEvents();
		Assert.IsFalse( window.Exists );
	}

	private static SDL_Event KeyEvent( SDL_Scancode scan, bool down ) => new()
	{
		key = new SDL_KeyboardEvent { type = down ? SDL_EventType.SDL_EVENT_KEY_DOWN : SDL_EventType.SDL_EVENT_KEY_UP, scancode = scan, down = down }
	};

	[TestMethod]
	public void ShortPressAndClickSurviveAndTransitionsKeepTheirOrder()
	{
		var input = new SdlInputSnapshot();
		input.Process( KeyEvent( SDL_Scancode.SDL_SCANCODE_LEFT, true ) );
		input.Process( KeyEvent( SDL_Scancode.SDL_SCANCODE_LEFT, false ) );
		input.Process( new SDL_Event { button = new SDL_MouseButtonEvent { type = SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN, button = 1, down = true } } );
		input.Process( new SDL_Event { button = new SDL_MouseButtonEvent { type = SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP, button = 1, down = false } } );
		Input.UpdateFrom( input );
		Assert.IsTrue( Input.Pressed( Key.Left ) ); Assert.IsTrue( Input.LeftPressed );
		Assert.IsFalse( Input.Keyboard.KeysDown.Contains( Key.Left ) ); Assert.IsFalse( Input.Mouse.Left );
		input.Process( KeyEvent( SDL_Scancode.SDL_SCANCODE_LEFT, true ) );
		Input.UpdateFrom( input );
		Assert.IsTrue( Input.Keyboard.KeysDown.Contains( Key.Left ) );
		input.BeginFrame(); Input.UpdateFrom( input );
		Assert.IsFalse( Input.Pressed( Key.Left ) ); Assert.IsFalse( Input.LeftPressed );
		input.Process( new SDL_Event { type = (uint)SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST } );
		Input.UpdateFrom( input );
		Assert.IsFalse( Input.Keyboard.KeysDown.Contains( Key.Left ) );
	}

	[TestMethod]
	public void Utf8TextWheelAndModifiersAreIndependentAndResetEachFrame()
	{
		var input = new SdlInputSnapshot();
		var pointer = Marshal.StringToCoTaskMemUTF8( "Ab中文𠮷" );
		try { input.Process( new SDL_Event { text = new SDL_TextInputEvent { type = SDL_EventType.SDL_EVENT_TEXT_INPUT, text = (byte*)pointer } } ); }
		finally { Marshal.FreeCoTaskMem( pointer ); }
		var ev = KeyEvent( SDL_Scancode.SDL_SCANCODE_V, true ); ev.key.mod = SDL_Keymod.SDL_KMOD_CTRL; input.Process( ev );
		input.Process( new SDL_Event { wheel = new SDL_MouseWheelEvent { type = SDL_EventType.SDL_EVENT_MOUSE_WHEEL, y = 2 } } );
		input.Process( new SDL_Event { wheel = new SDL_MouseWheelEvent { type = SDL_EventType.SDL_EVENT_MOUSE_WHEEL, y = 0.5f, direction = SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED } } );
		Input.UpdateFrom( input );
		Assert.AreEqual( "Ab中文𠮷", Input.Text ); Assert.AreEqual( 1.5f, Input.Mouse.Wheel );
		Assert.AreEqual( ModifierKeys.Control, input.KeyEvents[0].Modifiers );
		input.BeginFrame(); Input.UpdateFrom( input ); Assert.AreEqual( "", Input.Text ); Assert.AreEqual( 0f, Input.Mouse.Wheel );
		input.Process( new SDL_Event { type = (uint)SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST } ); Input.UpdateFrom( input );
	}

	[TestMethod]
	public void KeyRangesAndCommandKeysMapToGameKeys()
	{
		Assert.AreEqual( Key.A, SdlInputSnapshot.MapKey( SDL_Scancode.SDL_SCANCODE_A ) );
		Assert.AreEqual( Key.Z, SdlInputSnapshot.MapKey( SDL_Scancode.SDL_SCANCODE_Z ) );
		Assert.AreEqual( Key.Number0, SdlInputSnapshot.MapKey( SDL_Scancode.SDL_SCANCODE_0 ) );
		Assert.AreEqual( Key.F24, SdlInputSnapshot.MapKey( SDL_Scancode.SDL_SCANCODE_F24 ) );
		Assert.AreEqual( Key.Keypad9, SdlInputSnapshot.MapKey( SDL_Scancode.SDL_SCANCODE_KP_9 ) );
		Assert.AreEqual( Key.ControlRight, SdlInputSnapshot.MapKey( SDL_Scancode.SDL_SCANCODE_RCTRL ) );
		Assert.AreEqual( Key.Grave, SdlInputSnapshot.MapKey( SDL_Scancode.SDL_SCANCODE_GRAVE ) );
		Assert.AreEqual( Key.Unknown, SdlInputSnapshot.MapKey( SDL_Scancode.SDL_SCANCODE_UNKNOWN ) );
	}
}
