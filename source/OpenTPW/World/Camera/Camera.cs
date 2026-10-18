using System.Numerics;

namespace OpenTPW;

public static class Camera
{
	public static float FieldOfView => CameraMode.FieldOfView;
	public static Vector3 Position => CameraMode?.Position ?? Vector3.Zero;
	public static Rotation Rotation => CameraMode?.Rotation ?? Rotation.Identity;

	public static Matrix4x4 ViewMatrix { get; set; }
	public static Matrix4x4 ProjMatrix { get; set; }

	public static Matrix4x4 Matrix => ViewMatrix * ProjMatrix;

	private static CameraMode CameraMode = new();

	private static void CalcViewProjMatrix()
	{
		if ( CameraMode == null )
			return;

		var up = Vector3.Up;
		var direction = CameraMode.Rotation.Forward;
		var position = CameraMode.Position;

		ViewMatrix = Matrix4x4.CreateLookTo( 
			position.GetSystemVector3(), 
			direction.GetSystemVector3(), 
			up.GetSystemVector3()
		);

		var fieldOfView = CameraMode.FieldOfView;

		ProjMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
			fieldOfView.DegreesToRadians(),
			Screen.Aspect,
			0.1f,
			2000.0f
		);
	}

	public static void SetCameraMode<T>() where T : CameraMode
	{
		CameraMode = Activator.CreateInstance<T>();
	}

	public static void Update()
	{
		CameraMode.Update();

		// Run view/proj matrix calculations
		CalcViewProjMatrix();
	}
}
