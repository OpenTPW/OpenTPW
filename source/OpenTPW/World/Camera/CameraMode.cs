namespace OpenTPW;

public class CameraMode
{
	public Vector3 Position { get; set; } = new();
	public Rotation Rotation { get; set; } = Rotation.Identity;

	public float FieldOfView { get; set; } = 90f;

	public virtual void Update()
	{

	}
}
