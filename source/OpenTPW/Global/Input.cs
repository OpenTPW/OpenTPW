using Veldrid;
using Veldrid.Sdl2;

namespace OpenTPW;

public static partial class Input
{
	public static MouseInfo Mouse { get; internal set; } = new();
	public static KeyboardInfo Keyboard { get; internal set; } = new();
	/// <summary>Characters typed since the last frame, separate from key events.</summary>
	public static string Text { get; private set; } = "";
	/// <summary>Left button went down since the last frame, including a press released within the same frame.</summary>
	public static bool LeftPressed { get; private set; }
	/// <summary>Input frame counter; pages use it to ignore the click that opened them.</summary>
	public static ulong FrameNumber { get; private set; }
	private static HashSet<Key> pressedKeys = new();
	/// <summary>Key went down since the last frame (key-down events, not polled state).</summary>
	public static bool Pressed( Key key ) => pressedKeys.Contains( key );

	public static float Forward { get; set; }
	public static float Right { get; set; }

	internal static List<InputButton> LastKeysDown { get; set; } = new();
	internal static List<InputButton> KeysDown { get; set; } = new();

	private static CursorTypes _cursorType;
	public static CursorTypes CursorType
	{
		get => _cursorType;
		set => _cursorType = value;
	}

	public static bool IsSystemCursorVisible
	{
		get => Sdl2Native.SDL_ShowCursor( -1 ) == 1;
		set => Sdl2Native.SDL_ShowCursor( value ? 1 : 0 );
	}

	public static bool Pressed( InputButton button )
	{
		return KeysDown.Contains( button ) && !LastKeysDown.Contains( button );
	}

	public static bool Down( InputButton button )
	{
		return KeysDown.Contains( button );
	}

	public static bool Released( InputButton button )
	{
		return !KeysDown.Contains( button ) && LastKeysDown.Contains( button );
	}

	public struct KeyboardInfo
	{
		public List<Key> KeysDown { get; set; }

		public KeyboardInfo()
		{
			KeysDown = new();
		}

		public KeyboardInfo( List<Key> keysDown )
		{
			KeysDown = keysDown;
		}
	}

	private static Dictionary<InputButton, Key[]> Bindings = new();

	static Input()
	{
		static T? GetAttributeOfType<T>( Enum enumVal ) where T : System.Attribute
		{
			var type = enumVal.GetType();
			var memInfo = type.GetMember( enumVal.ToString() );
			var attributes = memInfo[0].GetCustomAttributes( typeof( T ), false );
			return (attributes.Length > 0) ? (T)attributes[0] : null;
		}

		//
		// Build bindings table based on [DefaultKey] attribute values
		//
		foreach ( var key in Enum.GetValues<InputButton>() )
		{
			var attrib = GetAttributeOfType<DefaultKeyAttribute>( key );

			if ( attrib == null )
				continue;

			Bindings.Add( key, attrib.Keys );
		}
	}

#if DEBUG
	/// <summary>Window position the acceptance run hovers over instead of the system mouse; the OS cursor is never moved.</summary>
	internal static Vector2? PointerOverride { get; set; }
	/// <summary>One simulated left click for the acceptance run, consumed by the next input update.</summary>
	internal static bool ClickOverride { get; set; }
#endif

	public static void UpdateFrom( InputSnapshot inputSnapshot )
	{
		FrameNumber++;
		IsSystemCursorVisible = true;

		var mousePos = new Vector2( inputSnapshot.MousePosition.X, inputSnapshot.MousePosition.Y );
#if DEBUG
		if ( PointerOverride is { } pointer ) mousePos = pointer;
#endif
		var mouseInfo = new MouseInfo
		{
			Delta = Mouse.Position - mousePos,
			Position = mousePos,
			Left = inputSnapshot.IsMouseDown( MouseButton.Left ),
			Right = inputSnapshot.IsMouseDown( MouseButton.Right ),
			Wheel = inputSnapshot.WheelDelta
		};

		// A press and release may both arrive between frames; retain the event even when polling is up.
		LeftPressed = inputSnapshot.MouseEvents.Any( mouse => mouse.MouseButton == MouseButton.Left && mouse.Down ) || mouseInfo.Left && !Mouse.Left;
#if DEBUG
		if ( ClickOverride ) { LeftPressed = true; ClickOverride = false; }
#endif
		Mouse = mouseInfo;
		Text = new string( inputSnapshot.KeyCharPresses.ToArray() );
		// Retain key-down events even when a short press is released in the same snapshot.
		// Text entry and command keys are separate SDL streams; neither should consume the other.
		pressedKeys = inputSnapshot.KeyEvents.Where( key => key.Down ).Select( key => key.Key ).ToHashSet();

		Right = 0;
		Forward = 0;

		var newKeysDown = inputSnapshot.KeyEvents.Where( x => x.Down ).Select( x => x.Key );
		var newKeysUp = inputSnapshot.KeyEvents.Where( x => !x.Down ).Select( x => x.Key );

		Keyboard = new KeyboardInfo(
			Keyboard.KeysDown.Concat( newKeysDown )
				.Distinct()
				.Where( x => !newKeysUp.Contains( x ) )
				.ToList()
		);

		bool IsKeyPressed( Key k ) => Keyboard.KeysDown.Contains( k );

		if ( IsKeyPressed( Key.A ) )
			Right -= 1;
		if ( IsKeyPressed( Key.D ) )
			Right += 1;
		if ( IsKeyPressed( Key.W ) )
			Forward += 1;
		if ( IsKeyPressed( Key.S ) )
			Forward -= 1;
		
		LastKeysDown = [.. KeysDown];
		KeysDown.Clear();

		foreach ( var (button, vkeys) in Bindings )
		{
			bool isPressed = true;

			foreach ( var vkey in vkeys )
			{
				if ( !IsKeyPressed( vkey ) )
					isPressed = false;
			}

			if ( isPressed )
			{
				KeysDown.Add( button );
			}
		}
	}
}
