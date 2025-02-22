namespace OpenTPW;

public class PlayerSelectCameraMode : CameraMode
{
	private float Height => 25.0f;
	private float Distance => 70f;
	private float Speed => 0.1f;

	private Vector3 CameraPosition { get; set; }
	private Vector3 Target { get; set; }

	private int PositionIndex = 0;

	Vector3[] _islands = [
		new Vector3( 400, 600, 35.0f ), // Space
		new Vector3( 600, 400, 38.0f ), // Hallow
		new Vector3( 600, 600, 17.5f ), // Fantasy
		new Vector3( 400, 400, 12.5f ), // Jungle
	];

	private Vector3 PreviousCameraPosition = Vector3.Zero;
	private Vector3 TargetCameraPosition => _islands[PositionIndex];

	private void SwitchIsland()
	{
		var oldPositionIndex = PositionIndex;

		do
		{
			PositionIndex = Random.Shared.Next( _islands.Length );
		} while ( PositionIndex == oldPositionIndex );

		PreviousCameraPosition = _islands[oldPositionIndex];
		Target = _islands[PositionIndex];

		TimeSinceIslandSwitch = 0;
	}

	public PlayerSelectCameraMode()
	{
		SwitchIsland();
		CameraPosition = Target;
	}

	TimeSince TimeSinceIslandSwitch;

	private float GetT()
	{
		float t = TimeSinceIslandSwitch / 20f;
		t = t.Clamp( 0, 1 );
		return Easings.EaseInOut( t );
	}

	public override void Update()
	{
		CameraPosition = PreviousCameraPosition.LerpTo( TargetCameraPosition, GetT() );

		float x = MathF.Sin( Time.Now * Speed ) * Distance;
		float y = MathF.Cos( Time.Now * Speed ) * Distance;

		float z = (MathF.Sin( Time.Now * Speed * 2f ) + 2f) * Height;

		Position = new Vector3( CameraPosition.X + x, CameraPosition.Y + y, z + CameraPosition.Z );
		Rotation = Rotation.LookAt( CameraPosition - Position );

		FieldOfView = 60;

		if ( TimeSinceIslandSwitch > 25 )
			SwitchIsland();
	}
}
