using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

internal sealed class IslandSelectionPanel : Panel
{
	private readonly LobbyScene scene;
	private static readonly string[] ControlAssets = ["b_worldview", "b_entpark", "lobleft", "lobrght"];
	private readonly ButtonHoverEffect[] hover = [new(), new(), new(), new()];
	private static readonly Texture helpBackground = new( new byte[] { 0, 15, 22, 205 }, 1, 1 );
	private int pressed = -1;

	private float Scale => Math.Min( Screen.RawSize.X / 800f, Screen.RawSize.Y / 600f );

	private Rectangle Rect( float x, float y, float w, float h ) => new( new Vector2( x * Scale, Screen.RawSize.Y - (600 - y) * Scale ), new Vector2( w, h ) * Scale );
	private Rectangle Control( int index ) => index switch
	{
		0 => Rect( 26, 373, 74, 74 ),
		1 => Rect( 26, 432, 74, 74 ),
		2 => Rect( 26, 500, 90, 90 ),
		_ => Rect( 94, 500, 90, 90 )
	};

	public IslandSelectionPanel( LobbyScene scene ) => this.scene = scene;

	// Island lobby overlays are authored in X/Z rather than regular UI X/Y.
	private void DrawModel( string name, Rectangle rect )
	{
		// The key and numeral contain mutually exclusive mesh states.
		var model = UiAssets.GetModel( name, Vector2.One, UiModelPlane.XZ,
			firstMeshOnly: name != "islandlobby", flipVertical: name != "s_key" );
		UiDrawing.Model( model, rect );
	}

	private int HoveredControl()
	{
		for ( int i = 0; i < ControlAssets.Length; i++ )
		{
			if ( Control( i ).Contains( Input.Mouse.Position ) )
			{
				return i;
			}
		}

		return -1;
	}

	protected override void OnRender()
	{
		DrawModel( "islandlobby", Rect( 8, 332, 178, 260 ) );
		for ( int i = 0; i < ControlAssets.Length; i++ )
		{
			var rect = Control( i );
			UiDrawing.Sprite( ControlAssets[i] + (pressed == i && Input.Mouse.Left ? "d" : ""), rect );
			hover[i].Draw( rect, CanReceiveInput && rect.Contains( Input.Mouse.Position ), Scale );
		}

		DrawModel( "s_key", new Rectangle( 363 * Scale, 62 * Scale, 175 * Scale, 104 * Scale ) );
		DrawModel( "s_X1", new Rectangle( 587 * Scale, 90 * Scale, 105 * Scale, 51 * Scale ) );
		KeyCounter.Draw( KeyCounter.InitialCount, Scale );
		MsdfText.Draw( scene.IslandName,
			new Vector2( Screen.RawSize.X / 2, Screen.RawSize.Y - 73 * Scale ),
			GameFont.MenuSmall,
			new RgbaFloat( 1, .9f, 0, 1 ),
			Scale,
			TextAlignment.Center );
		int hot = HoveredControl();
		string help = hot switch
		{
			0 => "Left-click to view all islands",
			1 => "Left-click to enter this island",
			2 => "Left-click to view previous island",
			3 => "Left-click to view next island",
			_ => "Select an island"
		};

		if ( hot >= 0 )
		{
			UiDrawing.Sprite( helpBackground, new Rectangle( (Screen.RawSize.X - 356 * Scale) / 2,
				Screen.RawSize.Y - 24 * Scale, 356 * Scale, 21 * Scale ) );

			MsdfText.Draw( help,
				new Vector2( Screen.RawSize.X / 2, Screen.RawSize.Y - 21 * Scale ),
				GameFont.GameSmall,
				RgbaFloat.White,
				Scale * .8f,
				TextAlignment.Center );
		}

		MsdfText.Draw( "v 2.0",
			new Vector2( Screen.RawSize.X - 39 * Scale, Screen.RawSize.Y - 29 * Scale ),
			GameFont.GameSmall,
			new RgbaFloat( .65f, .85f, .9f, 1 ),
			Scale * .65f );
	}

	protected override void OnUpdate()
	{
		if ( !CanReceiveInput )
		{
			return;
		}

		if ( Input.KeyPressed( Key.Escape ) )
		{
			scene.ShowPlayers();
			return;
		}

		if ( Input.KeyPressed( Key.Enter ) )
		{
			scene.EnterPark();
			return;
		}

		if ( Input.KeyPressed( Key.Left ) )
		{
			scene.SwitchIsland( -1 );
		}

		if ( Input.KeyPressed( Key.Right ) )
		{
			scene.SwitchIsland( 1 );
		}

		if ( Input.LeftPressed )
		{
			pressed = HoveredControl();
		}

		if ( !Input.LeftReleased )
		{
			return;
		}

		int activated = pressed;
		pressed = -1;
		if ( activated < 0 || !Control( activated ).Contains( Input.Mouse.Position ) )
		{
			return;
		}

		switch ( activated )
		{
			case 0:
				scene.ToggleOverview();
				break;
			case 1:
				scene.EnterPark();
				break;
			case 2:
				scene.SwitchIsland( -1 );
				break;
			case 3:
				scene.SwitchIsland( 1 );
				break;
		}
	}
}
