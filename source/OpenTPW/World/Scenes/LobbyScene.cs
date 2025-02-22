using OpenTPW.UI;
using System.Diagnostics.CodeAnalysis;

namespace OpenTPW;

public class LobbyScene
{
	internal static LobbyScene Current { get; set; } = null!;

	public RootPanel Hud { get; set; }
	public SunActor SunLight { get; set; }

	public LobbyScene()
	{
		Current = this;

		SetupHud();
		SetupEntities();
	}

	[MemberNotNull( nameof( Hud ), nameof( SunLight ) )]
	private void SetupEntities()
	{
		SunLight = new SunActor() { Position = new( 0, 100, 100 ) };

		_ = new WaterActor();
		_ = new SkyActor();

		foreach ( var file in FileSystem.GetFiles( "/lobby/" ) )
		{
			Log.Info( file );
		}

		// _ = new TestShit( new Vector3( -850, -135, 100 ), "ui/handle.md2" );
		// _ = new TestShit( new Vector3( 0, 0, 0 ), "ui/mainpanel.md2" );

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
		Actor.All.ToList().ForEach( entity => entity.Update() );

		Cursor.Update();
	}

	private Cursor Cursor = new();

	public void RenderUI()
	{
		Actor.All.ToList().ForEach( entity => entity.RenderUI() );

		//Cursor.Draw();
	}

	public void Render()
	{
		Actor.All.ToList().ForEach( entity => entity.Render() );
	}
}
