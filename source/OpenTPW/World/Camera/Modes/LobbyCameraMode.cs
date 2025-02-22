
namespace OpenTPW;

public class LobbyCameraMode : CameraMode
{
	private float Height => 50.0f;
	private float Distance => 70f;
	private float Speed => 0.2f;

	private Vector3 Target => new Vector3( 600, 400, 38.0f );

	public override void Update()
	{
		float x = MathF.Sin( Time.Now * Speed ) * Distance;
		float y = MathF.Cos( Time.Now * Speed ) * Distance;

		Position = new Vector3( Target.X + x, Target.Y + y, Height );
		Rotation = Rotation.LookAt( Target - Position );

		FieldOfView = 60;
	}
}
