namespace OpenTPW.UI;

/// <summary>Retail UI message codes (the values the engine queues with Ui_SendMessage, US 0x658ccc / TPI 0x8503cc).</summary>
public enum UiMessageKind
{
	/// <summary>A button was clicked; the page receives the button's identifier.</summary>
	Click = 0x100,
	Enter = 0x10001,
	Leave = 0x10002,
	Move = 0x10003,
	ButtonUp = 0x10004,
	ButtonDown = 0x10005,
	/// <summary>
	/// 0x10008: sent to the parent on each repeat interval while an auto-repeat button is held, and to the button itself
	/// when the pointer re-enters it while held. <see cref="UiMessage.Target"/> is always the button here.
	/// </summary>
	Repeat = 0x10008,
	/// <summary>Repeat delay elapsed: auto-repeat has started (retail sends it to the button's parent).</summary>
	RepeatStart = 0x10009,
}

public readonly record struct UiMessage( UiMessageKind Kind, UiElement Target, int Button = 0, int X = 0, int Y = 0 );

/// <summary>Differences between the two games' button handlers (US 0x668f9c / TPI 0x85b901).</summary>
public sealed record UiInputOptions
{
	/// <summary>
	/// After the left button is released, Theme Park World always relights the button's hover state; Theme Park Inc does
	/// so only if the pointer was still inside when it was released.
	/// </summary>
	public bool RelightHoverAfterOutsideRelease { get; init; }

	public static UiInputOptions ThemeParkWorld { get; } = new() { RelightHoverAfterOutsideRelease = true };
	public static UiInputOptions ThemeParkInc { get; } = new() { RelightHoverAfterOutsideRelease = false };
}

/// <summary>
/// The four-plus-two visual states a button model shows, as UiModel_GetControlState (US 0x668820 / TPI 0x85af60)
/// returns them.
/// </summary>
public enum UiControlState
{
	Normal = 0,
	Disabled = 1,
	Hover = 2,
	HoverSelected = 3,
	Pressed = 4,
	Selected = 5,
}

/// <summary>
/// Pointer routing and button behaviour of the retail UI core. Hover: Ui_UpdatePointerHover (US 0x6588ef /
/// TPI 0x84ffef) hit-tests the root and sends leave/enter when the element under the pointer changes, move otherwise;
/// a captured element gets every move. Buttons: Ui_DispatchPointerButton (US 0x658af1 / TPI 0x8501f1) sends button-down
/// to the hovered element and remembers it per button; the matching release (US 0x658b5b / TPI 0x85025b) goes to that
/// element. Button elements (class 2) then follow the button handler (US 0x668f9c / TPI 0x85b901).
/// Messages are queued and processed in order, as the retail queue is.
/// </summary>
public sealed class UiInput( UiInputOptions options )
{
	/// <summary>Repeat delay and interval a new button starts with (button constructor, US 0x6688b3 / TPI 0x85aff3).</summary>
	public const int DefaultRepeatDelay = 500, DefaultRepeatInterval = 125;

	private readonly Dictionary<UiElement, ButtonState> buttons = new( ReferenceEqualityComparer.Instance );
	private readonly Dictionary<int, UiElement?> pressed = [];
	private readonly Queue<UiMessage> queue = new();
	/// <summary>The single global press state of the button handler: 0 none, 1 held inside, 2 held but outside.</summary>
	private int pressPhase;
	private int now;

	public UiElement? Root { get; set; }
	public UiElement? Hovered { get; private set; }
	/// <summary>An element that receives every move while set (retail capture slot).</summary>
	public UiElement? Capture { get; set; }

	/// <summary>Every processed message, low-level and generated (click, repeat), in order.</summary>
	public event Action<UiMessage>? Message;
	/// <summary>Help hooks the button handler calls on enter, leave and move (a global help object in both games).</summary>
	public event Action<UiElement>? HelpEnter;
	public event Action? HelpLeave;

	private sealed class ButtonState
	{
		/// <summary>Bit 0 selected, bit 1 pressed (button +0x144 in TPI).</summary>
		public int Bits;
		/// <summary>Hover lit (+0x145 in TPI).</summary>
		public bool Hover;
		/// <summary>Held by the left button (+0x11c bit 0).</summary>
		public bool Held;
		public int RepeatDelay = DefaultRepeatDelay, RepeatInterval = DefaultRepeatInterval;
		/// <summary>0 idle, 1 waiting for the delay, 2 repeating (+0x16a).</summary>
		public int RepeatPhase;
		public int RepeatSince;
	}

	private ButtonState Button( UiElement e ) => buttons.TryGetValue( e, out var s ) ? s : buttons[e] = new ButtonState();

	/// <summary>UiButton_SetRepeatTiming (US 0x668f35 / TPI 0x85b753): delay before repeating and the interval after it, in milliseconds.</summary>
	public void SetRepeatTiming( UiElement button, int delay, int interval ) => (Button( button ).RepeatDelay, Button( button ).RepeatInterval) = (delay, interval);

	public void SetSelected( UiElement button, bool selected )
	{
		var s = Button( button );
		s.Bits = selected ? s.Bits | 1 : s.Bits & ~1;
	}

	public bool IsSelected( UiElement button ) => (Button( button ).Bits & 1) != 0;

	/// <summary>UiModel_GetControlState (US 0x668820 / TPI 0x85af60).</summary>
	public UiControlState StateOf( UiElement button )
	{
		if ( !button.Enabled ) return UiControlState.Disabled;
		var s = Button( button );
		if ( s.Hover ) return (s.Bits & 1) != 0 ? UiControlState.HoverSelected : UiControlState.Hover;
		if ( (s.Bits & 2) != 0 ) return UiControlState.Pressed;
		return (s.Bits & 1) != 0 ? UiControlState.Selected : UiControlState.Normal;
	}

	/// <summary>Pointer moved to (x, y) in 2048x1536 units.</summary>
	public void PointerMove( int x, int y )
	{
		if ( Root == null ) return;
		if ( Capture != null ) Send( new UiMessage( UiMessageKind.Move, Capture, 0, x, y ) );
		else
		{
			var hit = Root.HitTest( x, y );
			if ( ReferenceEquals( hit, Hovered ) ) { if ( hit != null ) Send( new UiMessage( UiMessageKind.Move, hit, 0, x, y ) ); }
			else
			{
				if ( Hovered != null ) Send( new UiMessage( UiMessageKind.Leave, Hovered ) );
				Hovered = hit;
				if ( hit != null ) Send( new UiMessage( UiMessageKind.Enter, hit, 0, x, y ) );
			}
		}
		Flush();
	}

	/// <summary>Button <paramref name="button"/> (0 left, 1 right) went down.</summary>
	public void PointerDown( int button, int x, int y )
	{
		pressed[button] = Hovered;
		if ( Hovered != null ) Send( new UiMessage( UiMessageKind.ButtonDown, Hovered, button, x, y ) );
		Flush();
	}

	public void PointerUp( int button, int x, int y )
	{
		if ( pressed.Remove( button, out var target ) && target != null ) Send( new UiMessage( UiMessageKind.ButtonUp, target, button, x, y ) );
		Flush();
	}

	/// <summary>Advances the UI clock; held auto-repeat buttons act on it (retail message 0x1e to the button).</summary>
	public void Tick( int milliseconds )
	{
		now = milliseconds;
		foreach ( var (element, s) in buttons )
			if ( s.Held ) RepeatTick( element, s );
		Flush();
	}

	private void Send( UiMessage message ) => queue.Enqueue( message );

	private void Flush()
	{
		while ( queue.TryDequeue( out var m ) )
		{
			Message?.Invoke( m );
			if ( m.Target.Type == 2 ) ButtonHandler( m );
		}
	}

	/// <summary>The class-2 button handler (US 0x668f9c / TPI 0x85b901).</summary>
	private void ButtonHandler( UiMessage m )
	{
		var e = m.Target;
		var s = Button( e );
		switch ( m.Kind )
		{
			case UiMessageKind.Enter:
				HelpEnter?.Invoke( e );
				if ( !s.Held ) s.Hover = true;
				else
				{
					if ( pressPhase == 2 ) pressPhase = 1;
					s.Bits |= 2;
					// Re-entering while held: the retail handler sends 0x10008 to the button itself.
					Send( new UiMessage( UiMessageKind.Repeat, e, 0, m.X, m.Y ) );
				}
				break;
			case UiMessageKind.Leave:
				HelpLeave?.Invoke();
				if ( !s.Held ) s.Hover = false;
				else
				{
					if ( pressPhase == 1 ) pressPhase = 2;
					s.Bits &= ~2;
				}
				break;
			case UiMessageKind.ButtonDown when m.Button == 0:
				if ( pressPhase != 0 ) break;
				pressPhase = 1;
				s.Held = true;
				s.Hover = false;
				s.Bits |= 2;
				if ( (e.Flags & UiElement.FlagRepeat) != 0 ) { s.RepeatSince = now; s.RepeatPhase = 1; }
				break;
			case UiMessageKind.ButtonUp when m.Button == 0:
			{
				// A click needs the press to have stayed inside and auto-repeat not to have started. The repeat phase is read
				// and cleared only when the press is inside (short-circuit in US 0x668f9c; TPI 0x85b71d does the reset).
				bool click = false;
				if ( pressPhase == 1 ) { click = s.RepeatPhase != 2; s.RepeatPhase = 0; }
				if ( (e.Flags & UiElement.FlagToggle) == 0 ) s.Bits = 0;
				else if ( (s.Bits & 2) != 0 ) s.Bits = (s.Bits & 1) == 0 ? 1 : 0;
				s.Held = false;
				if ( pressPhase != 2 || options.RelightHoverAfterOutsideRelease ) s.Hover = true;
				pressPhase = 0;
				if ( click ) Send( new UiMessage( UiMessageKind.Click, e ) );
				break;
			}
		}
	}

	/// <summary>Auto-repeat on the clock message (US 0x668dd9 / TPI 0x85b5f7); only while the button shows pressed.</summary>
	private void RepeatTick( UiElement e, ButtonState s )
	{
		if ( (e.Flags & UiElement.FlagRepeat) == 0 ) return;
		if ( s.RepeatPhase == 1 && s.RepeatDelay <= now - s.RepeatSince )
		{
			s.RepeatPhase = 2;
			s.RepeatSince = now;
			if ( (s.Bits & 2) != 0 ) Send( new UiMessage( UiMessageKind.RepeatStart, e ) );
		}
		else if ( s.RepeatPhase == 2 && s.RepeatInterval < now - s.RepeatSince )
		{
			if ( (s.Bits & 2) != 0 ) Send( new UiMessage( UiMessageKind.Repeat, e ) );
			s.RepeatSince = now;
		}
	}
}
