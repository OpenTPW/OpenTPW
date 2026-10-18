using System.Numerics;

namespace OpenTPW;

public sealed class Transform
{
	public Vector3 Position = Vector3.Zero;
	public Rotation Rotation = Rotation.Identity;
	public Vector3 Scale = Vector3.One;

	public static Transform Default => new();

	public Matrix4x4 GetMatrix() => CreateMatrix( Position, Rotation.GetSystemQuaternion(), Scale );

	internal static Matrix4x4 CreateMatrix( Vector3 position, Quaternion rotation, Vector3 scale )
	{
		var matrix = Matrix4x4.Identity;
		matrix *= Matrix4x4.CreateScale( scale.GetSystemVector3() );
		matrix *= Matrix4x4.CreateFromQuaternion( rotation );
		matrix *= Matrix4x4.CreateTranslation( position.GetSystemVector3() );

		return matrix;
	}
}
