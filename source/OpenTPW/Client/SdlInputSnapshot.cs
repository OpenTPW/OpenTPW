using System.Runtime.InteropServices;
using SDL;

namespace OpenTPW;

/// <summary>SDL3 events retained as game-owned input transitions and text.</summary>
internal sealed unsafe class SdlInputSnapshot : InputSnapshot
{
	private readonly List<KeyEvent> keys = new();
	private readonly List<MouseEvent> buttons = new();
	private readonly List<char> text = new();
	private readonly HashSet<Key> heldKeys = new();
	private readonly HashSet<MouseButton> heldButtons = new();
	public IReadOnlyList<KeyEvent> KeyEvents => keys;
	public IReadOnlyList<MouseEvent> MouseEvents => buttons;
	public IReadOnlyList<char> KeyCharPresses => text;
	public System.Numerics.Vector2 MousePosition { get; set; }
	public float WheelDelta { get; private set; }
	public bool IsMouseDown( MouseButton button ) => heldButtons.Contains( button );

	internal void BeginFrame()
	{
		keys.Clear(); buttons.Clear(); text.Clear(); WheelDelta = 0;
	}

	internal void Process( SDL_Event ev )
	{
		switch ( (SDL_EventType)ev.type )
		{
			case SDL_EventType.SDL_EVENT_KEY_DOWN:
			case SDL_EventType.SDL_EVENT_KEY_UP:
				var key = MapKey( ev.key.scancode );
				if ( key == Key.Unknown ) break;
				bool down = ev.key.down;
				var modifiers = ModifierKeys.None;
				if ( (ev.key.mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0 ) modifiers |= ModifierKeys.Shift;
				if ( (ev.key.mod & SDL_Keymod.SDL_KMOD_CTRL) != 0 ) modifiers |= ModifierKeys.Control;
				if ( (ev.key.mod & SDL_Keymod.SDL_KMOD_ALT) != 0 ) modifiers |= ModifierKeys.Alt;
				if ( (ev.key.mod & SDL_Keymod.SDL_KMOD_GUI) != 0 ) modifiers |= ModifierKeys.Gui;
				keys.Add( new KeyEvent( key, down, modifiers, ev.key.repeat ) );
				if ( down ) heldKeys.Add( key ); else heldKeys.Remove( key );
				break;
			case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
			case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
				var button = ev.button.button switch
				{
					1 => MouseButton.Left, 2 => MouseButton.Middle, 3 => MouseButton.Right,
					4 => MouseButton.Button1, 5 => MouseButton.Button2,
					6 => MouseButton.Button3, 7 => MouseButton.Button4, 8 => MouseButton.Button5,
					_ => (MouseButton?)null
				};
				if ( button is not { } b ) break;
				bool buttonDown = ev.button.down;
				buttons.Add( new MouseEvent( b, buttonDown ) );
				if ( buttonDown ) heldButtons.Add( b ); else heldButtons.Remove( b );
				break;
			case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
				WheelDelta += ev.wheel.direction == SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED ? -ev.wheel.y : ev.wheel.y;
				break;
			case SDL_EventType.SDL_EVENT_TEXT_INPUT:
				// SDL owns this UTF-8 pointer only until the next poll; decode it immediately.
				text.AddRange( Marshal.PtrToStringUTF8( (IntPtr)ev.text.text ) ?? "" );
				break;
			case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST:
				// A release outside this window must not leave navigation or dragging stuck.
				foreach ( var held in heldKeys ) keys.Add( new KeyEvent( held, false, ModifierKeys.None ) );
				foreach ( var held in heldButtons ) buttons.Add( new MouseEvent( held, false ) );
				heldKeys.Clear(); heldButtons.Clear();
				break;
		}
	}

	internal static Key MapKey( SDL_Scancode scan ) => scan switch
	{
		>= SDL_Scancode.SDL_SCANCODE_A and <= SDL_Scancode.SDL_SCANCODE_Z => Key.A + (scan - SDL_Scancode.SDL_SCANCODE_A),
		>= SDL_Scancode.SDL_SCANCODE_1 and <= SDL_Scancode.SDL_SCANCODE_9 => Key.Number1 + (scan - SDL_Scancode.SDL_SCANCODE_1),
		>= SDL_Scancode.SDL_SCANCODE_F1 and <= SDL_Scancode.SDL_SCANCODE_F12 => Key.F1 + (scan - SDL_Scancode.SDL_SCANCODE_F1),
		>= SDL_Scancode.SDL_SCANCODE_F13 and <= SDL_Scancode.SDL_SCANCODE_F24 => Key.F13 + (scan - SDL_Scancode.SDL_SCANCODE_F13),
		>= SDL_Scancode.SDL_SCANCODE_KP_1 and <= SDL_Scancode.SDL_SCANCODE_KP_9 => Key.Keypad1 + (scan - SDL_Scancode.SDL_SCANCODE_KP_1),
		SDL_Scancode.SDL_SCANCODE_0 => Key.Number0,
		SDL_Scancode.SDL_SCANCODE_APOSTROPHE => Key.Quote,
		SDL_Scancode.SDL_SCANCODE_APPLICATION => Key.Menu,
		SDL_Scancode.SDL_SCANCODE_BACKSLASH => Key.BackSlash,
		SDL_Scancode.SDL_SCANCODE_BACKSPACE => Key.BackSpace,
		SDL_Scancode.SDL_SCANCODE_CAPSLOCK => Key.CapsLock,
		SDL_Scancode.SDL_SCANCODE_CLEAR => Key.Clear,
		SDL_Scancode.SDL_SCANCODE_COMMA => Key.Comma,
		SDL_Scancode.SDL_SCANCODE_DELETE => Key.Delete,
		SDL_Scancode.SDL_SCANCODE_DOWN => Key.Down,
		SDL_Scancode.SDL_SCANCODE_END => Key.End,
		SDL_Scancode.SDL_SCANCODE_EQUALS => Key.Plus,
		SDL_Scancode.SDL_SCANCODE_ESCAPE => Key.Escape,
		SDL_Scancode.SDL_SCANCODE_GRAVE => Key.Grave,
		SDL_Scancode.SDL_SCANCODE_HOME => Key.Home,
		SDL_Scancode.SDL_SCANCODE_INSERT => Key.Insert,
		SDL_Scancode.SDL_SCANCODE_KP_0 => Key.Keypad0,
		SDL_Scancode.SDL_SCANCODE_KP_DIVIDE => Key.KeypadDivide,
		SDL_Scancode.SDL_SCANCODE_KP_ENTER => Key.KeypadEnter,
		SDL_Scancode.SDL_SCANCODE_KP_MINUS => Key.KeypadMinus,
		SDL_Scancode.SDL_SCANCODE_KP_MULTIPLY => Key.KeypadMultiply,
		SDL_Scancode.SDL_SCANCODE_KP_PERIOD => Key.KeypadPeriod,
		SDL_Scancode.SDL_SCANCODE_KP_PLUS => Key.KeypadPlus,
		SDL_Scancode.SDL_SCANCODE_LALT => Key.AltLeft,
		SDL_Scancode.SDL_SCANCODE_LCTRL => Key.ControlLeft,
		SDL_Scancode.SDL_SCANCODE_LEFT => Key.Left,
		SDL_Scancode.SDL_SCANCODE_LEFTBRACKET => Key.BracketLeft,
		SDL_Scancode.SDL_SCANCODE_LGUI => Key.WinLeft,
		SDL_Scancode.SDL_SCANCODE_LSHIFT => Key.ShiftLeft,
		SDL_Scancode.SDL_SCANCODE_MINUS => Key.Minus,
		SDL_Scancode.SDL_SCANCODE_NONUSBACKSLASH => Key.NonUSBackSlash,
		SDL_Scancode.SDL_SCANCODE_NUMLOCKCLEAR => Key.NumLock,
		SDL_Scancode.SDL_SCANCODE_PAGEDOWN => Key.PageDown,
		SDL_Scancode.SDL_SCANCODE_PAGEUP => Key.PageUp,
		SDL_Scancode.SDL_SCANCODE_PAUSE => Key.Pause,
		SDL_Scancode.SDL_SCANCODE_PERIOD => Key.Period,
		SDL_Scancode.SDL_SCANCODE_PRINTSCREEN => Key.PrintScreen,
		SDL_Scancode.SDL_SCANCODE_RALT => Key.AltRight,
		SDL_Scancode.SDL_SCANCODE_RCTRL => Key.ControlRight,
		SDL_Scancode.SDL_SCANCODE_RETURN => Key.Enter,
		SDL_Scancode.SDL_SCANCODE_RGUI => Key.WinRight,
		SDL_Scancode.SDL_SCANCODE_RIGHT => Key.Right,
		SDL_Scancode.SDL_SCANCODE_RIGHTBRACKET => Key.BracketRight,
		SDL_Scancode.SDL_SCANCODE_RSHIFT => Key.ShiftRight,
		SDL_Scancode.SDL_SCANCODE_SCROLLLOCK => Key.ScrollLock,
		SDL_Scancode.SDL_SCANCODE_SEMICOLON => Key.Semicolon,
		SDL_Scancode.SDL_SCANCODE_SLASH => Key.Slash,
		SDL_Scancode.SDL_SCANCODE_SLEEP => Key.Sleep,
		SDL_Scancode.SDL_SCANCODE_SPACE => Key.Space,
		SDL_Scancode.SDL_SCANCODE_TAB => Key.Tab,
		SDL_Scancode.SDL_SCANCODE_UP => Key.Up,
		_ => Key.Unknown
	};
}
