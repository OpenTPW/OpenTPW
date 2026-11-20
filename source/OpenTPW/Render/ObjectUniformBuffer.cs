using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenTPW;

[StructLayout( LayoutKind.Sequential )]
struct ObjectUniformBuffer
{
	/*
	 * These fields are padded so that they're
	 * aligned (as blocks) to multiples of 16.
	 */

	public Matrix4x4 g_mModel; // 64
	public Matrix4x4 g_mView; // 64
	public Matrix4x4 g_mProj; // 64

	public Vector3 g_vLightPos; // 12
	public float _padding0; // 4

	public Vector3 g_vLightColor; // 12
	public float _padding1; // 4

	public Vector3 g_vCameraPos; // 12
	public float g_flTime; // 4

	public Vector4 g_vFogColor; // 16
}
