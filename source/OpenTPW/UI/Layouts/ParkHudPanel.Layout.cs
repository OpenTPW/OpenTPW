using Veldrid;

namespace OpenTPW.UI;

internal sealed partial class ParkHudPanel
{
	private enum ParkControl { Buy, Information, Finances, Research, Map, Camera }
	private sealed record HudButton( ParkControl Action, string Asset, string Help, Rectangle Bounds );

	// Reference artwork is laid out on a 600-pixel-high screen. Bottom panel
	// coordinates stay anchored to the bottom when the window aspect changes.
	// Positions and sizes below are visual tuning, not file-format requirements.
	private const float ReferenceHeight = 600;
	private const float HorizontalStretch = 1.1f;
	private static readonly HudButton[] Buttons =
	[
		new( ParkControl.Buy, "b_buy", "Buy attractions", new( 70, 466, 42, 42 ) ),
		new( ParkControl.Information, "b_info", "Park information", new( 114, 471, 42, 42 ) ),
		new( ParkControl.Finances, "b_money", "Finances", new( 70, 510, 42, 42 ) ),
		new( ParkControl.Research, "b_resrch", "Research", new( 114, 515, 42, 42 ) ),
		new( ParkControl.Map, "b_map", "Map", new( 40, 554, 42, 42 ) ),
		new( ParkControl.Camera, "b_camera", "Camera", new( 84, 559, 42, 42 ) )
	];

	private static readonly Rectangle MainPanelBounds = new( 8, 389, 147, 206 );
	private static readonly Rectangle HandleBounds = new( 101, 408, 72, 185 );
	private static readonly Rectangle DateBounds = new( 44, 414, 96, 26 );
	private static readonly Rectangle GaugeBounds = new( 22, 430, 26, 103 );
	private static readonly Rectangle AerialBounds = new( 22, 335, 48, 55 );
	private static readonly Rectangle CashIconBounds = new( 12, 14, 66, 78 );
	private static readonly Rectangle StatusPanelBounds = new( 156, 405, 280, 174 );
	private static readonly Vector2 DatePosition = new( 70, 420 );
	private static readonly Vector2 CashPosition = new( 94, 6 );
	private static readonly Vector2 PausedPosition = new( 190, 550 );
	private static readonly Vector2 HelpPosition = new( 165, 576 );
	private static readonly Vector2 StatusTitlePosition = new( 180, 422 );
	private static readonly Vector2 StatusBodyPosition = new( 180, 451 );
	private static readonly RgbaFloat DateColor = new( 0, .12f, .04f, 1 );
	private static readonly RgbaFloat TitleColor = new( 1, .9f, 0, 1 );
	private const float BodyTextScale = .8f;
	private const float CashTextScale = 1.1f;
	private const float CashOutlineWidth = 2;
	private const float StatusLineHeight = 24;
	private const float ButtonArtworkOffset = 20;
	private const float ButtonArtworkExpansion = 32;
	private const float MapSize = 190;
	private const float MapMargin = 12;

	private float Scale => Screen.RawSize.Y / ReferenceHeight;
	private float ScaleX => Scale * HorizontalStretch;

	private Vector2 BottomAlignedPoint( Vector2 point ) =>
		new( point.X * ScaleX, Screen.RawSize.Y - (ReferenceHeight - point.Y) * Scale );

	private Rectangle BottomAlignedRect( Rectangle bounds ) =>
		new( BottomAlignedPoint( new Vector2( bounds.X, bounds.Y ) ), new Vector2( bounds.Width * ScaleX, bounds.Height * Scale ) );

	private Vector2 TopLeftPoint( Vector2 point ) => new( point.X * ScaleX, point.Y * Scale );

	private Rectangle TopLeftRect( Rectangle bounds ) =>
		new( bounds.X * ScaleX, bounds.Y * Scale, bounds.Width * ScaleX, bounds.Height * Scale );
}
