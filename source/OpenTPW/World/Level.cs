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

		SetupEntities();
		SetupHud();
	}

	private void SetupEntities()
	{
		SunLight = new Sun() { Position = new( 0, 100, 100 ) };

		_ = new Water() { Scale = new Vector3( 10000f ) };
		_ = new Sky();

		_ = new LobbyIsland( new Vector3( 400, 400, 0 ), "Jungle" );
		_ = new LobbyIsland( new Vector3( 600, 400, 0 ), "Hallow" );
		_ = new LobbyIsland( new Vector3( 600, 600, 0 ), "Fantasy" );
		_ = new LobbyIsland( new Vector3( 400, 600, 0 ), "Space" );

		Camera.SetCameraMode<LobbyCameraMode>();
	}

	private void SetupHud()
	{
		Hud = new();

		var layout = new LobbyLayout() { Hud = Hud };
		layout.OnInit();

		Hud.AddChild( new Cursor() );
	}

	public void Update()
	{
		Entity.All.ForEach( entity => entity.Update() );
	}

	public void Render()
	{
		Camera.Update();

		Entity.All.ForEach( entity => entity.Render() );
	}
}
