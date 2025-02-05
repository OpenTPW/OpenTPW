using System.Numerics;

namespace OpenTPW;

public sealed class Transform
{
	public Vector3 Position = Vector3.Zero;
	public Rotation Rotation = Rotation.Identity;
	public Vector3 Scale = Vector3.One;

	public Matrix4x4 GetMatrix()
	{
		var matrix = Matrix4x4.Identity;
		matrix *= Matrix4x4.CreateScale( Scale.GetSystemVector3() );
		matrix *= Matrix4x4.CreateFromQuaternion( Rotation.GetSystemQuaternion() );
		matrix *= Matrix4x4.CreateTranslation( Position.GetSystemVector3() );

		return matrix;
	}
}
