using System.Numerics;

namespace OpenTPW;

partial class Graphics
{
	private static Point2 BaseScreenSize = new( 1280, 720 );
	private static Matrix4x4 CameraMatrix = Matrix4x4.Identity;

	public static Matrix4x4 ViewProjectionMatrix => CameraMatrix;

	public class Scope : IDisposable
	{
		Point2 _oldScreenSize;
		Matrix4x4 _oldCameraMatrix;

		public Scope( Point2 newScreenSize, Matrix4x4? cameraMatrix )
		{
			_oldScreenSize = BaseScreenSize;
			_oldCameraMatrix = CameraMatrix;
			BaseScreenSize = newScreenSize;
			CameraMatrix = cameraMatrix ?? CameraMatrix;
		}

		public Scope( Point2 newScreenSize ) : this( newScreenSize, null )
		{
		}

		void IDisposable.Dispose()
		{
			BaseScreenSize = _oldScreenSize;
			CameraMatrix = _oldCameraMatrix;
		}
	}
}
