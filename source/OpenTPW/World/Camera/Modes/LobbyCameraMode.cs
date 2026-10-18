namespace OpenTPW;

public class LobbyCameraMode : CameraMode
{
	// Original lobby.txt specifies spin speed .02 and radius 70. The height
	// and overview view below are reconstructed framing for these assets.
	private const float OrbitSpeed = .02f;
	private const float OrbitRadius = 70;
	private const float OrbitHeight = 35;
	private const float FocusFollowRate = 5;
	private const float HorizontalFieldOfView = 90;
	private const float OverviewFieldOfView = 75;
	private static readonly Vector3 OverviewPosition = new( 500, 180, 310 );
	private static readonly Vector3 OverviewFocus = new( 500, 500, 0 );
	private readonly float started = Time.Now;
	private Vector3 focus;
	private bool initialized;

	public override void Update()
	{
		var scene = LobbyScene.Current;
		var target = scene.IslandTarget;
		if ( !initialized )
		{
			focus = target;
			initialized = true;
		}

		focus = focus.LerpTo( target, Math.Min( 1, Time.Delta * FocusFollowRate ) );
		float angle = (Time.Now - started) * OrbitSpeed - MathF.PI / 2;
		if ( scene.IsOverview )
		{
			Position = OverviewPosition;
			Rotation = Rotation.LookAt( OverviewFocus - Position );
			FieldOfView = OverviewFieldOfView;
			return;
		}

		Position = new Vector3( focus.X + MathF.Cos( angle ) * OrbitRadius, focus.Y + MathF.Sin( angle ) * OrbitRadius, focus.Z + OrbitHeight );
		Rotation = Rotation.LookAt( focus - Position );
		// Preserve horizontal framing across aspect ratios. The renderer takes vertical FOV.
		FieldOfView = 2 * MathF.Atan( MathF.Tan( (HorizontalFieldOfView / 2) * MathF.PI / 180 ) / Screen.Aspect ) * 180 / MathF.PI;
	}
}
