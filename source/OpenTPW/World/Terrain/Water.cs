using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenTPW;

public class Water : ModelEntity
{
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
		public float g_flTime;
		public System.Numerics.Vector4 g_vFogColor;
	}

	public override void Spawn()
	{
		var texture = new Texture( "lobby/terrain/textures/jri_lak3.wct", TextureFlags.Wrap );

		var material = new Material<ObjectUniformBuffer>( "content/shaders/water.shader" );
		material.Set( "Color", texture );

		Model = Primitives.Plane.GenerateModel( material );
	}

	protected override void OnRender()
	{
		if ( Model == null )
			return;

		var uniformBuffer = new ObjectUniformBuffer
		{
			g_mModel = ModelMatrix,
			g_mView = Camera.ViewMatrix,
			g_mProj = Camera.ProjMatrix,
			g_vLightPos = Level.SunLight.Position,
			g_vLightColor = Level.SunLight.Color,
			g_vCameraPos = Camera.Position,

			_padding0 = 0,
			_padding1 = 0,
			g_flTime = Time.Now,
			g_vFogColor = new System.Numerics.Vector4( (Level.EnvironmentSky?.Color ?? new Vector3( 0.301f, 0.84f, 1 )).GetSystemVector3(), 1 )
		};

		Model.Material.Set( "ObjectUniformBuffer", uniformBuffer );
		Model.Draw();
	}
}
