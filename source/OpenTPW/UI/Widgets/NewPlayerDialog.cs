using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

internal sealed class NewPlayerDialog : Dialog
{
	private readonly PlayerProfiles profiles;
	private readonly int slot;
	private readonly Action<PlayerProfile> created;
	private string name = "";
	private string error = "";
	private PlayerGameMode mode = PlayerGameMode.FullSimulation;
	private readonly ButtonHoverEffect instantHover = new();
	private readonly ButtonHoverEffect simulationHover = new();

	public NewPlayerDialog( PlayerProfiles profiles, int slot, Action<PlayerProfile> created ) : base( "#EnterName" )
	{
		this.profiles = profiles;
		this.slot = slot;
		this.created = created;
		IsWave = true;
	}

	private Rectangle InstantRect => Rect( 178, 87, 292, 34 );

	private Rectangle SimulationRect => Rect( 178, 135, 292, 34 );

	protected override void RenderContents()
	{
		Frame( "f_frame", Rect( 160, 72, 329, 112 ), 10, 16 );
		ModePanelSurface( Rect( 169, 81, 311, 94 ) );
		ModeButton( "#InstantAction", InstantRect, mode == PlayerGameMode.InstantAction );
		ModeButton( "#FullSimulation", SimulationRect, mode == PlayerGameMode.FullSimulation );
		Frame( "f_frame", Rect( 63, 197, 419, 42 ), 10, 8 );
		string display = name.Length == 0 ? Localization.Parse( "#TypeYourName" ) : name;
		Text( display, 78, 205, .88f );
		if ( Time.Now % 1 < .5f )
		{
			float advance = MsdfText.Measure( display, GameFont.GameBold, .88f ).X;
			Text( "|", 78 + advance, 205, .88f, color: new RgbaFloat( .15f, .75f, .85f, 1 ) );
		}

		if ( mode == PlayerGameMode.InstantAction )
		{
			Sprite( "e_joy", Rect( 514, 193, 64, 64 ) );
		}

		Text( error, 305, 248, .65f, TextAlignment.Center );
	}

	private void ModeButton( string key, Rectangle rect, bool selected )
	{
		var hovered = CanReceiveInput && rect.Contains( Input.Mouse.Position );
		var down = hovered && Input.LeftDown;
		var texture = down ? "b_textd" : selected ? "b_togglel" : "b_text";
		Frame( texture, rect, 8, 8 );
		Text( Localization.Parse( key ),
			(rect.X + rect.Width / 2 - Origin.X) / Scale,
			(rect.Y + 6 * Scale - Origin.Y) / Scale,
			1.08f,
			TextAlignment.Center,
			GameFont.GameMedium,
			color: selected ? new RgbaFloat( .14f, .2f, .02f, 1 ) : RgbaFloat.White );
		(key == "#InstantAction" ? instantHover : simulationHover).Draw( rect, hovered, Scale );
	}

	protected override void OnUpdate()
	{
		UpdateName();
		if ( Input.KeyPressed( Key.Tab ) || Input.KeyPressed( Key.Left ) || Input.KeyPressed( Key.Right ) )
		{
			mode = mode == PlayerGameMode.FullSimulation ? PlayerGameMode.InstantAction : PlayerGameMode.FullSimulation;
		}

		if ( Input.LeftPressed )
		{
			if ( InstantRect.Contains( Input.Mouse.Position ) )
			{
				mode = PlayerGameMode.InstantAction;
			}

			if ( SimulationRect.Contains( Input.Mouse.Position ) )
			{
				mode = PlayerGameMode.FullSimulation;
			}
		}

		base.OnUpdate();
	}

	private void UpdateName()
	{
		foreach ( var e in Input.KeyEvents.Where( e => e.Down ) )
		{
			if ( e.Key == Key.BackSpace && name.Length > 0 )
			{
				name = name[..^1];
			}
		}

		foreach ( char c in Input.TextInput )
		{
			if ( !char.IsControl( c ) && name.Length < PlayerProfiles.MaxNameLength )
			{
				name += c;
				error = "";
			}
		}
	}

	protected override void Confirm()
	{
		PlayerProfile profile;
		try
		{
			profile = profiles.Create( slot, name, mode );
		}
		catch ( Exception ex ) when ( ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException )
		{
			error = ex.Message;
			return;
		}

		Delete();
		created( profile );
	}
}
