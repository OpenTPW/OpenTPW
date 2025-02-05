namespace OpenTPW;

public class TestCameraMode : CameraMode
{
	public override void Update()
	{
		Position = (Vector3.Up * 700) + Vector3.Forward * 640 + Vector3.Right * 512;
		Rotation = Rotation.From( 90, 0, 0 );
		FieldOfView = 60;
	}
}
