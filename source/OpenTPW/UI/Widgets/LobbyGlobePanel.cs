using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

/// <summary>Shared globe controls from 0x755238 and the dispatch at 0x4c2cc0.</summary>
internal sealed class LobbyGlobePanel : Panel
{
	private readonly Level level;
	private readonly LobbyUiModel background;
	private readonly Dictionary<int, (LobbyUiModel Model, Rectangle Bounds)> buttons = new();
	private readonly LobbyUiText status;
	private readonly HashSet<Entity> entities = new();
	private LobbyModel? globe;
	private long sound2, sound3;
	private bool rightDown;
	private int heldButton;
	private float lastRepeat;
	private ulong activatedFrame;
	public bool Active { get; private set; }
	public LobbyGlobeMode Mode { get; private set; }
	public LobbyGlobeState CameraState { get; }
	public bool Owns( Entity entity ) => entities.Contains( entity );
	private static Rectangle Box( float l, float t, float r, float b ) => LobbyNamingPanel.Box( l, t, r, b );
	public LobbyGlobePanel( Level level )
	{
		this.level = level;
		CameraState = new LobbyGlobeState( level.Lobby! );
		background = new( LobbyControlResources.ResolveModel( 0x0d83e731 ), Box( 9, 620, 510, 1531 ) );
		void Button( int id, uint hash, Rectangle bounds ) => buttons.Add( id, (new LobbyUiModel( LobbyControlResources.ResolveModel( hash ), bounds ), bounds) );
		Button( 0x16000, 0x1d1bc312, Box( 49, 898, 202, 1051 ) );
		Button( 0x16001, 0x76b2cc3c, Box( 184, 1327, 337, 1481 ) );
		Button( 0x16002, 0x795da8e1, Box( 61, 1201, 214, 1355 ) );
		Button( 0x16003, 0x390d8a1b, Box( 49, 731, 202, 885 ) );
		Button( 0x16004, 0xf1d0542a, Box( 307, 1201, 460, 1355 ) );
		Button( 0x16005, 0xee357a41, Box( 182, 1077, 336, 1231 ) );
		Button( 0x16006, 0x19de04e0, Box( 41, 1070, 143, 1173 ) );
		status = new( "", Box( 520, 1380, 2047, 1530 ), LobbyFonts.Name( 4 ) );
	}
	public void Show( LobbyGlobeMode mode )
	{
		if ( Active ) return;
		if ( globe == null )
		{
			var before = Entity.All.ToHashSet();
			globe = new LobbyModel( Vector3.Zero, "lobby/globe/globe.md2", "lobby/globe/textures" );
			entities.UnionWith( Entity.All.Where( entity => !before.Contains( entity ) ) );
		}
		Mode = mode; Active = true; globe.Visible = true; activatedFrame = Input.FrameNumber;
		CameraState.Reset();
		LobbyGlobeCameraMode.State = CameraState;
		Camera.SetCameraMode<LobbyGlobeCameraMode>();
		level.IslandMenu!.Active = false;
		level.LobbySound?.Dispose();
		level.Advisor?.Cancel();
		sound2 = level.Audio?.Play( "global/sound", "cat_globallobbysfx", 2 ) ?? 0;
		sound3 = level.Audio?.Play( "global/sound", "cat_globallobbysfx", 3 ) ?? 0;
		rightDown = Input.Mouse.Right;
		// 0x5b4910/0x5b4860 request City::GetCityList even on the deferred connection path.
		// No local city records are shipped by this path, so do not manufacture map markers.
		status.Text = Localization.Parse( "#Disconnected" );
		Log.Info( "Globe opened offline; online city and park lists are unavailable." );
	}
	public void Hide()
	{
		if ( !Active ) return;
		// The globe owns both global loops. Restore island ambience on the next Level update.
		Active = false; globe!.Visible = false;
		level.Audio?.Stop( sound2 ); level.Audio?.Stop( sound3 ); sound2 = sound3 = 0;
		Camera.SetCameraMode<LobbyCameraMode>();
		level.IslandMenu!.Activate();
		level.Advisor?.Say( 0x258, true );
	}
	protected override void OnUpdate()
	{
		if ( !Active || activatedFrame == Input.FrameNumber ) return;
		if ( Input.Pressed( Key.Escape ) ) { Hide(); return; }
		if ( Input.Pressed( Key.Left ) ) CameraState.Turn( -1, 0 );
		if ( Input.Pressed( Key.Right ) ) CameraState.Turn( 1, 0 );
		if ( Input.Pressed( Key.Up ) ) CameraState.Turn( 0, 1 );
		if ( Input.Pressed( Key.Down ) ) CameraState.Turn( 0, -1 );
		if ( Input.Mouse.Right && !rightDown ) CameraState.ToggleZoom();
		rightDown = Input.Mouse.Right;
		if ( !Input.Mouse.Left ) heldButton = 0;
		foreach ( var (id, button) in buttons )
		{
			if ( Mode == LobbyGlobeMode.Select && id is 0x16000 or 0x16003 ) continue;
			// The local lobby has no online city provider. Do not expose an enabled list action with no result.
			if ( id == 0x16003 ) { button.Model.State = 1; continue; }
			bool hover = LobbyNamingPanel.Hit( button.Bounds );
			bool selected = id == 0x16006 && CameraState.Zoomed;
			button.Model.State = hover ? Input.Mouse.Left || selected ? 3 : 2 : selected ? 5 : 0;
			if ( !hover ) continue;
			if ( Input.LeftPressed )
			{
				if ( id == 0x16000 ) { Hide(); return; }
				if ( id == 0x16006 ) CameraState.ToggleZoom();
				heldButton = id; lastRepeat = float.NegativeInfinity;
			}
			// 0x6633b5 sets delay=0/interval=10 ms; 0x663259 emits at most one repeat per update.
			if ( heldButton != id || !Input.Mouse.Left || Time.Now - lastRepeat <= 0.01f ) continue;
			lastRepeat = Time.Now;
			if ( id == 0x16001 ) CameraState.Turn( 0, -1 );
			else if ( id == 0x16002 ) CameraState.Turn( -1, 0 );
			else if ( id == 0x16004 ) CameraState.Turn( 1, 0 );
			else if ( id == 0x16005 ) CameraState.Turn( 0, 1 );
		}
	}
	protected override void OnRender()
	{
		if ( !Active ) return;
		// Select mode omits the visit-only return/list models from the authored control table.
		background.Draw();
		foreach ( var (id, button) in buttons )
			if ( Mode != LobbyGlobeMode.Select || id is not (0x16000 or 0x16003) ) button.Model.Draw();
		status.Draw();
	}
}
