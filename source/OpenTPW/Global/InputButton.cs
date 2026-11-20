
namespace OpenTPW;

public enum InputButton
{
	/// <summary>
	/// Editor Toggle (`)
	/// Toggles the editor, if enabled.
	/// </summary>
	[DefaultKey( Key.Grave )]
	EditorToggle,

	/// <summary>
	/// Open Park (Ctrl + O)
	/// Opens the park.
	/// </summary>
	[DefaultKey( Key.ControlLeft, Key.O )]
	OpenPark,

	/// <summary>
	/// Close Park (Ctrl + C)
	/// Closes the park.
	/// </summary>
	[DefaultKey( Key.ControlLeft, Key.C )]
	ClosePark,

	/// <summary>
	/// Toggle Help Bar (Ctrl + H)
	/// Turns the help bar on or off.
	/// </summary>
	[DefaultKey( Key.ControlLeft, Key.H )]
	ToggleHelpBar,

	/// <summary>
	/// Menu (Esc)
	/// Accesses the menu or cancels the current action.
	/// </summary>
	[DefaultKey( Key.Escape )]
	Menu,

	/// <summary>
	/// Buy Attractions (B)
	/// Buys attractions for the park.
	/// </summary>
	[DefaultKey( Key.B )]
	BuyAttractions,

	/// <summary>
	/// Hire Staff (H)
	/// Hires staff for the park.
	/// </summary>
	[DefaultKey( Key.H )]
	HireStaff,

	/// <summary>
	/// Park Info (I)
	/// Shows park information or status.
	/// </summary>
	[DefaultKey( Key.I )]
	ParkInfo,

	/// <summary>
	/// All Attractions (A)
	/// Shows all attractions in the park.
	/// </summary>
	[DefaultKey( Key.A )]
	AllAttractions,

	/// <summary>
	/// All Staff (S)
	/// Shows all staff in the park.
	/// </summary>
	[DefaultKey( Key.S )] 
	AllStaff,

	/// <summary>
	/// All Visitors (V)
	/// Shows all visitors in the park.
	/// </summary>
	[DefaultKey( Key.V )]
	AllVisitors,

	/// <summary>
	/// Financial Info (F)
	/// Shows financial information of the park.
	/// </summary>
	[DefaultKey( Key.F )] 
	FinancialInfo,

	/// <summary>
	/// Loans (L)
	/// Manages loans.
	/// </summary>
	[DefaultKey( Key.L )]
	Loans,

	/// <summary>
	/// Staff Budgets (T)
	/// Manages staff training budgets.
	/// </summary>
	[DefaultKey( Key.T )]
	StaffBudgets,

	/// <summary>
	/// Entry Price (G)
	/// Sets or changes the entry price for the park.
	/// </summary>
	[DefaultKey( Key.G )]
	EntryPrice,

	/// <summary>
	/// Research (R)
	/// Manages park research.
	/// </summary>
	[DefaultKey( Key.R )]
	Research,

	/// <summary>
	/// Map Screen (Space)
	/// Opens the map screen.
	/// </summary>
	[DefaultKey( Key.Space )]
	MapScreen,

	/// <summary>
	/// Staff Locator (Ctrl + S)
	/// Locates staff in the park.
	/// </summary>
	[DefaultKey( Key.ControlLeft, Key.S )]
	StaffLocator,

	/// <summary>
	/// Visitor Locator (Ctrl + V)
	/// Locates visitors in the park.
	/// </summary>
	[DefaultKey( Key.ControlLeft, Key.V )]
	VisitorLocator,

	/// <summary>
	/// Send Email Postcard (Ctrl + P)
	/// Sends an E-Mail postcard.
	/// </summary>
	[DefaultKey( Key.ControlLeft, Key.P )]
	SendEmailPostcard,

	/// <summary>
	/// Camcorder Mode (C)
	/// Enables camcorder mode, click to desired location.
	/// </summary>
	[DefaultKey( Key.C )]
	CamcorderMode,

	/// <summary>
	/// Rotate Left (Left Arrow)
	/// Rotates the camera left.
	/// </summary>
	[DefaultKey( Key.Left )]
	RotateLeft,

	/// <summary>
	/// Rotate Right (Right Arrow)
	/// Rotates the camera right.
	/// </summary>
	[DefaultKey( Key.Right )]
	RotateRight,

	/// <summary>
	/// Scroll Left (Numpad 4)
	/// Scrolls the view left.
	/// </summary>
	[DefaultKey( Key.Keypad4 )]
	ScrollLeft,

	/// <summary>
	/// Scroll Right (Numpad 6)
	/// Scrolls the view right.
	/// </summary>
	[DefaultKey( Key.Keypad6 )]
	ScrollRight,

	/// <summary>
	/// Zoom In (Up Arrow)
	/// Zooms the camera in.
	/// </summary>
	[DefaultKey( Key.Up )]
	ZoomIn,

	/// <summary>
	/// Zoom Out (Down Arrow)
	/// Zooms the camera out.
	/// </summary>
	[DefaultKey( Key.Down )]
	ZoomOut,

	/// <summary>
	/// Home (Home)
	/// Returns to Park Gates View.
	/// </summary>
	[DefaultKey( Key.Home )]
	Home,

	/// <summary>
	/// Turn Blueprint Left (Comma)
	/// Turns the blueprint left.
	/// </summary>
	[DefaultKey( Key.Comma )]
	TurnBlueprintLeft,

	/// <summary>
	/// Turn Blueprint Right (Full stop)
	/// Turns the blueprint right.
	/// </summary>
	[DefaultKey( Key.Period )]
	TurnBlueprintRight,

	/// <summary>
	/// Clone (Hold Ctrl and left-click)
	/// Copies an object (hold key and left-click the object).
	/// </summary>
	[DefaultKey( Key.ControlLeft )]
	Clone,

	/// <summary>
	/// Delete (Backspace)
	/// Deletes queues/tracks etc.
	/// </summary>
	[DefaultKey( Key.BackSpace )]
	Delete,

	/// <summary>
	/// Clear (Delete)
	/// Clears land.
	/// </summary>
	[DefaultKey( Key.Delete )]
	Clear,

	/// <summary>
	/// Time Speed Up (Numpad +)
	/// Speeds up time.
	/// </summary>
	[DefaultKey( Key.KeypadPlus )]
	TimeSpeedUp,

	/// <summary>
	/// Time Slow Down (Numpad -)
	/// Slows down time.
	/// </summary>
	[DefaultKey( Key.KeypadMinus )]
	TimeSlowDown,

	/// <summary>
	/// Time Reset (Numpad *)
	/// Resets time to normal.
	/// </summary>
	[DefaultKey( Key.KeypadMultiply )]
	TimeReset,

	/// <summary>
	/// Pause (P)
	/// Pauses or unpauses the game.
	/// </summary>
	[DefaultKey( Key.P )]
	Pause,

	/// <summary>
	/// Help (F1)
	/// Plays advisor help speech for the current screen/action.
	/// </summary>
	[DefaultKey( Key.F1 )] 
	Help,
}
