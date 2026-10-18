using OpenTPW.UI;

namespace OpenTPW;

public class ParkCameraMode : CameraMode
{
	// Camera tuning uses world units, degrees, and units per second.
	private static readonly Vector3 EntranceViewOffset = new( 5, 75, 0 );
	private float RotationStepDegrees => 90;
	private float MovementSpeed => 80;
	private float WheelZoomStep => 16;
	private float MinimumHeight => 48;
	private float MaximumHeight => 640;
	private float HorizontalDistancePerHeight => .75f;
	private float ParkFieldOfView => 60;
	internal static Vector3 StartPosition { get; set; }
	internal static Vector2 Bounds { get; set; }
	private Vector3 target;
	private bool initialized;
	private float height = 65;
	private float yaw = 90;

	public override void Update()
	{
		if ( !initialized )
		{
			target = StartPosition + EntranceViewOffset;
			initialized = true;
		}

		if ( RootPanel.Instance?.Children.Any( panel => panel.IsModal ) == true || LoadingScreen.Visible )
		{
			return;
		}

		if ( Input.Pressed( InputButton.RotateLeft ) )
		{
			yaw -= RotationStepDegrees;
		}

		if ( Input.Pressed( InputButton.RotateRight ) )
		{
			yaw += RotationStepDegrees;
		}

		var heading = Rotation.From( 0, yaw, 0 );
		var direction = heading.Forward * Input.Forward + heading.Right * Input.Right;
		target += direction.WithZ( 0 ) * Time.Delta * MovementSpeed;
		target.X = target.X.Clamp( 0, Bounds.X );
		target.Y = target.Y.Clamp( 0, Bounds.Y );
		height = (height - Input.Mouse.Wheel * WheelZoomStep).Clamp( MinimumHeight, MaximumHeight );

		var targetPostion = target - heading.Forward * height * HorizontalDistancePerHeight + Vector3.Up * height;
		var targetRotation = Rotation.LookAt( target - Position );

		Position = Position.LerpTo( targetPostion, Time.Delta * 50f );
		Rotation = Rotation.LerpTo( targetRotation, Time.Delta * 15f );
		FieldOfView = ParkFieldOfView;
	}
}
