namespace OpenTPW;

public class TestCameraMode : CameraMode
{
	public override void Update()
	{
		Position = Vector3.Up * 128f + Vector3.Backward * 64f;
		Rotation = Rotation.LookAt( -Position );
		FieldOfView = 90;
	}
}
