using OpenTPW.UI;
using Veldrid;
using Veldrid.Sdl2;

namespace OpenTPW;

/// <summary>Login name controls from the table at 0x74fa58; coordinates remain in retail UI units.</summary>
internal sealed class LobbyNamingPanel : Panel
{
	private readonly LobbyPlayerFlow flow;
	private readonly List<Panel> controls = new();
	private readonly LobbyUiText name;
	private readonly LobbyUiText error;
	private readonly Action<byte> confirm;
	private readonly Action cancel;
	private readonly LobbyUiModel instant;
	private readonly LobbyUiModel simulation;
	private readonly LobbyUiModel okay;
	private readonly LobbyUiModel exit;
	private readonly LobbyUiModel backdrop;
	private readonly LobbyUiText instantText, simulationText;
	private readonly LobbyNameEdit edit;
	private readonly LobbyUiModel selection, modeIcon;
	private float editedAt;
	private bool nameFocused;
	/// <summary>Elements whose help has already placed a MessageTag1 since the dialog opened (element property 0xa).</summary>
	private readonly HashSet<int> tagged = new();
	public byte CreationMode { get; private set; } = 1;
	public bool Active { get; set; }

	internal static Rectangle Box( float left, float top, float right, float bottom ) => new( left, top, right - left, bottom - top );
	internal static bool Hit( Rectangle units )
	{
		var point = Input.Mouse.Position;
		float x = point.X * 2048 / Screen.Size.X, y = point.Y * 1536 / Screen.Size.Y;
		return x >= units.X && x < units.X + units.Width && y >= units.Y && y < units.Y + units.Height;
	}

	public LobbyNamingPanel( LobbyPlayerFlow flow, Action<byte> confirm, Action cancel )
	{
		this.flow = flow;
		this.confirm = confirm;
		this.cancel = cancel;
		edit = new LobbyNameEdit( flow );
		selection = new LobbyUiModel( LobbyControlResources.ResolveModel( 0x06b50a59 ), Box( 623, 632, 631, 677 ) );
		modeIcon = new LobbyUiModel( LobbyControlResources.ResolveModel( 0x79490fde ), Box( 1241, 606, 1343, 708 ) );
		// 0x47cff0 creates the modal underlay separately from the authored dialog table.
		backdrop = new LobbyUiModel( "LOLIGHT", Box( 0, 0, 2047, 1535 ) );
		// Table 0x74fa58 binds 0xcd93131e; 0x48744c registers w_dialog_wave as "wdialogw".
		controls.Add( new LobbyUiModel( LobbyControlResources.ResolveModel( 0xcd93131e ), Box( 500, 233, 1516, 830 ) ) );
		controls.Add( new LobbyUiModel( "!f_plain", Box( 735, 377, 1235, 597 ) ) );
		controls.Add( new LobbyUiModel( "!frame", Box( 593, 616, 1216, 698 ) ) );
		controls.Add( instant = new LobbyUiModel( "b_ltoggle", Box( 764, 405, 1206, 476 ) ) );
		controls.Add( simulation = new LobbyUiModel( "b_ltoggle", Box( 764, 498, 1206, 569 ) ) );
		controls.Add( exit = new LobbyUiModel( "b_exit", Box( 1387, 613, 1470, 696 ) ) );
		controls.Add( okay = new LobbyUiModel( "b_okay", Box( 1376, 524, 1460, 608 ) ) );
		// 0x4a48c0 applies 0x490a90 to element 0x709.
		// UI-table text elements of the naming window (0x74fa58), drawn with their shadow.
		controls.Add( new LobbyUiText( Localization.Parse( "#EnterName" ), Box( 655, 285, 1315, 365 ), "TITLEMED", 0xeaef66ff ) { Shadow = true } );
		controls.Add( instantText = new LobbyUiText( Localization.Parse( "#InstantAction" ), Box( 764, 405, 1206, 476 ) ) { Shadow = true } );
		controls.Add( simulationText = new LobbyUiText( Localization.Parse( "#FullSimulation" ), Box( 764, 498, 1206, 569 ) ) { Shadow = true } );
		name = new LobbyUiText( "", Box( 623, 632, 1186, 677 ) ) { HorizontalAlignment = 0, VerticalAlignment = 0 };
		error = new LobbyUiText( "", Box( 500, 720, 1516, 830 ) );
		controls.Add( error );
	}
	public void Open()
	{
		// 0x4a48c0 sets UITEXT 0xf2 through 0x660839 and then selects the whole buffer.
		edit.Open( Localization.Parse( "#TypeYourName" ) );
		name.Text = edit.Text; name.Scroll = 0;
		nameFocused = true; Active = true; editedAt = Time.Now;
		CreationMode = 1;
		tagged.Clear();
	}

	/// <summary>
	/// UIHELPTEXT numbers from the naming table's key 0x11 (US 0x753f68–0x7540a8): Exit 2, OK 360, name 356,
	/// Instant Action 358, Full Simulation 359. A selected toggle answers key 0x12 instead (0x663890 reads
	/// +0x94 when +0x138 is 1, inferred to be the help lookup), which is −1 here.
	/// </summary>
	private static readonly (Rectangle Bounds, int Help, int Selected)[] helpElements =
	[
		(Box( 1387, 613, 1470, 696 ), 2, 2), (Box( 1376, 524, 1460, 608 ), 360, 360), (Box( 593, 616, 1216, 698 ), 356, 356),
		(Box( 764, 405, 1206, 476 ), 358, -1), (Box( 764, 498, 1206, 569 ), 359, -1),
	];

	/// <summary>
	/// Shows the hovered element's help (0x4924b0) and, on its first help in this dialog, places MessageTag1 at
	/// its centre (0x48b9f0 event 0x11 → 0x484e40(0x5a)).
	/// </summary>
	private void UpdateHelp()
	{
		var level = Level.Current;
		if ( level?.Help is not { } help ) return;
		int index = Array.FindIndex( helpElements, element => Hit( element.Bounds ) );
		int number = -1;
		if ( index >= 0 )
		{
			bool selected = index == 3 ? CreationMode == 1 : index == 4 && CreationMode == 0;
			number = selected ? helpElements[index].Selected : helpElements[index].Help;
		}
		if ( number >= 0 && tagged.Add( index ) ) level.KeyEffects?.MessageTag( helpElements[index].Bounds );
		help.Show( number );
	}

	/// <summary>Hides the help strip when the dialog closes.</summary>
	public void Close()
	{
		Active = false;
		Level.Current?.Help?.Show( -1 );
	}
	public void ShowError( string message ) => error.Text = message;
	protected override void OnUpdate()
	{
		if ( !Active ) return;
		if ( Input.Pressed( Key.Escape ) ) { cancel(); return; }
		bool control = Input.Keyboard.KeysDown.Contains( Key.ControlLeft ) || Input.Keyboard.KeysDown.Contains( Key.ControlRight );
		bool shift = Input.Keyboard.KeysDown.Contains( Key.ShiftLeft ) || Input.Keyboard.KeysDown.Contains( Key.ShiftRight );
		if ( nameFocused )
		{
			if ( control && Input.Pressed( Key.A ) ) edit.SelectAll();
			else if ( control && Input.Pressed( Key.V ) ) edit.Insert( Sdl2Native.SDL_GetClipboardText() );
			else if ( Input.Text.Length > 0 && !control ) edit.Insert( Input.Text );
			if ( Input.Pressed( Key.BackSpace ) ) edit.Delete( true );
			if ( Input.Pressed( Key.Delete ) ) edit.Delete( false );
			if ( Input.Pressed( Key.Left ) ) edit.Move( -1, shift );
			if ( Input.Pressed( Key.Right ) ) edit.Move( 1, shift );
			if ( Input.Pressed( Key.Home ) ) edit.MoveTo( 0, shift );
			if ( Input.Pressed( Key.End ) ) edit.MoveTo( edit.Text.Length, shift );
			if ( Input.Text.Length > 0 || Input.Pressed( Key.BackSpace ) || Input.Pressed( Key.Delete ) ) editedAt = Time.Now;
		}
		name.Text = flow.Name;
		float caret = name.Measure( edit.Text[..edit.Caret] ), width = 563 / 3.2f;
		if ( caret - name.Scroll > width - 8 / 3.2f ) name.Scroll = caret - width + 8 / 3.2f;
		if ( caret < name.Scroll ) name.Scroll = caret;
		// 0x662ca0: disabled=1, hover=2, hover+down=3, held=4, selected/down=5.
		int State( Rectangle bounds, bool selected = false, bool disabled = false ) => disabled ? 1 :
			Hit( bounds ) ? (selected || Input.Mouse.Left ? 3 : 2) : selected ? 5 : 0;
		instant.State = State( Box( 764, 405, 1206, 476 ), CreationMode == 1 );
		simulation.State = State( Box( 764, 498, 1206, 569 ), CreationMode == 0 );
		// 0x662d33 initializes white/black text colors; 0x662f53 selects them by button state.
		SetButtonText( instantText, instant.State );
		SetButtonText( simulationText, simulation.State );
		okay.State = State( Box( 1376, 524, 1460, 608 ) );
		exit.State = State( Box( 1387, 613, 1470, 696 ) );
		UpdateHelp();
		if ( Input.LeftPressed )
		{
			if ( Hit( Box( 593, 616, 1216, 698 ) ) )
			{
				float x = Input.Mouse.Position.X * 640 / Screen.Size.X - 623 / 3.2f + name.Scroll;
				int position = Enumerable.Range( 0, edit.Text.Length + 1 ).MinBy( index => Math.Abs( name.Measure( edit.Text[..index] ) - x ) );
				edit.MoveTo( position, shift ); nameFocused = true; editedAt = Time.Now;
			}
			else nameFocused = false;
			if ( Hit( Box( 764, 405, 1206, 476 ) ) ) CreationMode = 1;
			else if ( Hit( Box( 764, 498, 1206, 569 ) ) ) CreationMode = 0;
			else if ( Hit( Box( 1387, 613, 1470, 696 ) ) ) { cancel(); return; }
		}
		if ( flow.CanConfirm && (Input.Pressed( Key.Enter ) || Input.LeftPressed && Hit( Box( 1376, 524, 1460, 608 ) )) ) confirm( CreationMode );
	}
	private static void SetButtonText( LobbyUiText text, int state )
	{
		text.Color = state is 3 or 5 ? 0x000000ffu : 0xffffffffu;
		text.Offset = state is 3 or 5 ? new Vector2( 1, 1 ) : state == 4 ? new Vector2( 2, 1 ) : new Vector2();
	}
	protected override void OnRender()
	{
		if ( !Active ) return;
		backdrop.Draw();
		foreach ( var control in controls ) control.Draw();
		if ( CreationMode == 1 ) modeIcon.Draw();
		// The edit constructor 0x66051d registers a 0x2ee (750 ms) caret timer.
		if ( nameFocused && (edit.SelectionLength > 0 || (Time.Now - editedAt) % 1.5f < 0.75f) )
		{
			float left = name.Measure( edit.Text[..edit.SelectionStart] ) - name.Scroll;
			float right = edit.SelectionLength > 0 ? name.Measure( edit.Text[..(edit.SelectionStart + edit.SelectionLength)] ) - name.Scroll : left + 8 / 3.2f;
			left = Math.Clamp( left, 0, 563 / 3.2f ); right = Math.Clamp( right, 0, 563 / 3.2f );
			if ( right > left )
			{
				selection.Bounds = Box( 623 + left * 3.2f, 632, 623 + right * 3.2f, Math.Min( 677, 632 + name.LineHeight * 3.2f ) );
				selection.Draw();
			}
		}
		name.Draw();
	}
}
