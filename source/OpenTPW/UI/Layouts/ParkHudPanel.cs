using Veldrid;
using System.Globalization;

namespace OpenTPW.UI;

internal sealed partial class ParkHudPanel : Panel
{
	private readonly ParkScene scene;
	private readonly Action leave;
	private readonly ButtonHoverEffect[] hoverEffects = Buttons.Select( _ => new ButtonHoverEffect() ).ToArray();
	private readonly int initialCash;
	private Texture? map;
	private int pressedButton = -1;
	private int selectedPanel = -1;
	private bool mapVisible;

	// The park HUD currently shows initial status, before simulation is implemented.
	private const string StartingDate = "1.1.2000";
	private const int StartingAdmissionFee = 20;

	public ParkHudPanel( ParkScene scene, Action leave )
	{
		this.scene = scene;
		this.leave = leave;
		var settings = new SettingsFile( "levels/Standard.sam" );
		initialCash = int.Parse( settings["BankAccountInfo.InitialCash"], CultureInfo.InvariantCulture );
		foreach ( string name in new[] { "mainpanel", "date", "handle", "gauge", "i_dollar" } )
		{
			GetModel( name );
		}

		foreach ( var button in Buttons )
		{
			GetSprite( button.Asset );
			GetSprite( button.Asset + "d" );
		}
	}

	private static Model GetModel( string name )
	{
		// These particular models are authored in X/Z; other HUD models use X/Y.
		var plane = name is "gauge" or "i_dollar" or "s_key" ? UiModelPlane.XZ : UiModelPlane.XY;
		return UiAssets.GetModel( name, Vector2.One, plane, firstMeshOnly: true, flipVertical: name != "s_key" );
	}

	private static Texture GetSprite( string name ) => UiAssets.GetTexture( name, TextureFlags.PinkChromaKey );

	private static void DrawModel( string name, Rectangle bounds ) => UiDrawing.Model( GetModel( name ), bounds );

	private void DrawText( string text, Vector2 position, RgbaFloat? color = null, float size = BodyTextScale )
	{
		var screenPosition = BottomAlignedPoint( position );
		MsdfText.Draw( text, screenPosition, GameFont.GameSmall, color ?? RgbaFloat.White,
			Scale * size, horizontalScale: HorizontalStretch );
	}

	private int HoveredButton()
	{
		for ( int i = 0; i < Buttons.Length; i++ )
		{
			if ( BottomAlignedRect( Buttons[i].Bounds ).Contains( Input.Mouse.Position ) )
			{
				return i;
			}
		}

		return -1;
	}

	protected override void OnRender()
	{
		int hoveredButton = HoveredButton();
		DrawMainPanel();
		DrawParkStatus();
		DrawButtons( hoveredButton );
		DrawSelectedPanel();
		DrawMap();
		if ( hoveredButton >= 0 && CanReceiveInput )
		{
			DrawText( Buttons[hoveredButton].Help, HelpPosition );
		}
	}

	private void DrawMainPanel()
	{
		DrawModel( "mainpanel", BottomAlignedRect( MainPanelBounds ) );
		DrawModel( "handle", BottomAlignedRect( HandleBounds ) );
		DrawModel( "date", BottomAlignedRect( DateBounds ) );
		DrawText( StartingDate, DatePosition, DateColor, size: 1 );
		DrawModel( "gauge", BottomAlignedRect( GaugeBounds ) );
		UiDrawing.Sprite( GetSprite( "aerialtop" ), BottomAlignedRect( AerialBounds ) );
	}

	private void DrawParkStatus()
	{
		DrawModel( "i_dollar", TopLeftRect( CashIconBounds ) );
		MsdfText.Draw( initialCash.ToString( CultureInfo.InvariantCulture ), TopLeftPoint( CashPosition ),
			GameFont.CashBig, RgbaFloat.White, Scale * CashTextScale,
			outlineWidth: CashOutlineWidth * Scale, horizontalScale: HorizontalStretch );
		KeyCounter.Draw( KeyCounter.InitialCount, Scale, TextureFlags.PinkChromaKey );
		if ( scene.Paused )
		{
			DrawText( "Paused", PausedPosition );
		}
	}

	private void DrawButtons( int hoveredButton )
	{
		for ( int i = 0; i < Buttons.Length; i++ )
		{
			bool down = pressedButton == i && Input.Mouse.Left;
			var bounds = BottomAlignedRect( Buttons[i].Bounds );
			string asset = Buttons[i].Asset + (down ? "d" : "");
			// Sprite artwork extends beyond the clickable square.
			var artwork = new Rectangle( bounds.X - ButtonArtworkOffset * ScaleX,
				bounds.Y - ButtonArtworkOffset * Scale,
				bounds.Width + ButtonArtworkExpansion * ScaleX,
				bounds.Height + ButtonArtworkExpansion * Scale );
			UiDrawing.Sprite( GetSprite( asset ), artwork );
			hoverEffects[i].Draw( bounds, CanReceiveInput && hoveredButton == i, Scale );
		}
	}

	private void DrawSelectedPanel()
	{
		if ( selectedPanel < 0 )
		{
			return;
		}

		var button = Buttons[selectedPanel];
		DrawModel( "!f_plain", BottomAlignedRect( StatusPanelBounds ) );
		DrawText( button.Help, StatusTitlePosition, TitleColor, size: 1 );
		string text = button.Action switch
		{
			ParkControl.Buy => "No attractions built",
			ParkControl.Research => "No research in progress",
			ParkControl.Information => $"{scene.Theme}\nPark closed\nVisitors: 0",
			_ => $"Balance: {initialCash:N0}\nAdmission fee: {StartingAdmissionFee}"
		};
		var lines = text.Split( '\n' );
		for ( int i = 0; i < lines.Length; i++ )
		{
			DrawText( lines[i], StatusBodyPosition + new Vector2( 0, i * StatusLineHeight ) );
		}
	}

	private void DrawMap()
	{
		if ( !mapVisible || map == null )
		{
			return;
		}

		float side = MapSize * Scale;
		UiDrawing.Sprite( map, new Rectangle( Screen.RawSize.X - side - MapMargin * Scale,
			MapMargin * Scale, side, side ) );
	}

	private void ShowReturnDialog()
	{
		Parent!.AddChild( new Dialog( "Return to island selection?\n\nLeave this park and return to the lobby.", leave ) );
	}

	protected override void OnUpdate()
	{
		if ( !CanReceiveInput )
		{
			pressedButton = -1;
			return;
		}

		if ( Input.KeyPressed( Key.Escape ) )
		{
			CloseOverlayOrShowMenu();
			return;
		}

		if ( Input.KeyPressed( Key.Space ) )
		{
			scene.Paused = !scene.Paused;
		}

		if ( Input.LeftPressed )
		{
			pressedButton = HoveredButton();
		}

		if ( !Input.LeftReleased )
		{
			return;
		}

		int activated = pressedButton;
		pressedButton = -1;
		if ( activated >= 0 && activated == HoveredButton() )
		{
			ActivateButton( activated );
		}
	}

	private void CloseOverlayOrShowMenu()
	{
		if ( selectedPanel >= 0 || mapVisible )
		{
			selectedPanel = -1;
			mapVisible = false;
		}
		else
		{
			ShowReturnDialog();
		}
	}

	private void ActivateButton( int index )
	{
		switch ( Buttons[index].Action )
		{
			case ParkControl.Camera:
				Camera.SetCameraMode<ParkCameraMode>();
				break;
			case ParkControl.Map:
				ToggleMap();
				break;
			default:
				selectedPanel = selectedPanel == index ? -1 : index;
				break;
		}
	}

	private void ToggleMap()
	{
		mapVisible = !mapVisible;
		if ( mapVisible && map == null )
		{
			using var stream = FileSystem.OpenRead( $"levels/{scene.Theme}/2dmap.tga" );
			map = new Texture( stream );
		}
	}
}
