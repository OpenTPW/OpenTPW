using OpenTPW.UI;

namespace OpenTPW;

public class Level
{
	internal static Level Current { get; set; }

	public RootPanel Hud { get; set; }
	public Sun SunLight { get; set; }

	public SettingsFile Global { get; private init; }

	public Level( string levelName )
	{
		Global = new SettingsFile( $"/levels/{levelName}/global.sam" );
		Current = this;

		SetupHud();
		SetupEntities();
	}

	private void SetupEntities()
	{
		SunLight = new Sun() { Position = new( 0, 100, 100 ) };

		_ = new Water();
		_ = new Sky();

		// _ = new TestShit( new Vector3( -850, -135, 100 ), "ui/handle.md2" );
		// _ = new TestShit( new Vector3( 0, 0, 0 ), "ui/mainpanel.md2" );
		_ = new TestShit( "ui/LOLIGHT.md2" )
		{
			Position = new Vector3( -22300, 0, 0 ),
			Scale = new Vector3( 25 ),
		};

		_ = new TestShit( "ui/w_dialog_wave.md2" )
		{
			Position = new Vector3( -1000, 0, 0 )
		};

		_ = new LobbyIsland( new Vector3( 400, 400, 0 ), "Jungle" );
		_ = new LobbyIsland( new Vector3( 600, 400, 0 ), "Hallow" );
		_ = new LobbyIsland( new Vector3( 600, 600, 0 ), "Fantasy" );
		_ = new LobbyIsland( new Vector3( 400, 600, 0 ), "Space" );

		Camera.SetCameraMode<PlayerSelectCameraMode>();
	}

	private void SetupHud()
	{
		Hud = new();

		var layout = new LobbyLayout() { Hud = Hud };
		layout.OnInit();
	}

	public void Update()
	{
		Thing.All.ForEach( entity => entity.Update() );
	}

	private Cursor Cursor = new();

	public void RenderUI()
	{
		Thing.All.ForEach( entity => entity.RenderUI() );

		Cursor.Draw();
	}

	public void Render()
	{
		Thing.All.ForEach( entity => entity.Render() );
	}
}
