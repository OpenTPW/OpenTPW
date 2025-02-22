namespace OpenTPW;

public class ParkCameraMode : CameraMode
{
	private Vector3 wishVelocity = new();
	private Vector3 velocity = new();
	private float wishYaw;
	private bool wasPressed;
	private Vector2 mouseAnchor;
	private float cameraSpeed = 128f;
	private float wishHeight = 2f;
	private float yaw;

	public override void Update()
	{
		//
		// Get user input
		//
		var wishDir = new Vector3( Input.Forward, Input.Right, 0 ).Normal;

		if ( Input.Mouse.Right && !wasPressed )
		{
			mouseAnchor = Input.Mouse.Position;
		}

		if ( Input.Mouse.Right )
		{
			var delta = mouseAnchor - Input.Mouse.Position;
			wishDir = new Vector3( delta.Y, -delta.X, 0 ) / 512f;
		}

		wasPressed = Input.Mouse.Right;

		wishVelocity = Rotation.Forward * wishDir.X * Time.Delta * cameraSpeed;
		wishVelocity += Rotation.Right * wishDir.Y * Time.Delta * cameraSpeed;
		wishVelocity.Z = 0;

		wishHeight += -Input.Mouse.Wheel;
		wishHeight = wishHeight.Clamp( 1f, 10f );

		if ( Input.Pressed( InputButton.RotateLeft ) )
			wishYaw -= 90;
		if ( Input.Pressed( InputButton.RotateRight ) )
			wishYaw += 90;

		//
		// Apply everything
		//

		// Apply velocity
		velocity += wishVelocity * Time.Delta;

		// Apply drag
		velocity = velocity.LerpTo( Vector3.Zero, Time.Delta * 5f );

		// Rotate camera
		yaw = yaw.LerpTo( wishYaw, 10f * Time.Delta );

		// Move camera
		Position += velocity;
		Position = Position.WithZ( Position.Z.LerpTo( wishHeight, 10f * Time.Delta ) );
	}
}
