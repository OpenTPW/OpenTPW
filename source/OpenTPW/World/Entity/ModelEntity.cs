using Veldrid;

namespace OpenTPW;

public partial class ModelEntity : Entity
{
	public Model? Model { get; set; }

	public ModelEntity()
	{
		Spawn();
	}

	public virtual void Spawn()
	{

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
			g_vLightPos = Level.SunLight?.Position ?? Vector3.Zero,
			g_vLightColor = Level.SunLight?.Color ?? Vector3.One,
			g_vCameraPos = Camera.Position,
			g_flTime = Time.Now,

			_padding0 = 0,
			_padding1 = 0,
			g_vFogColor = new System.Numerics.Vector4( (Level.EnvironmentSky?.Color ?? new Vector3( 0.301f, 0.84f, 1 )).GetSystemVector3(), Level.FogEnabled ? 1 : 0 ),
		};

		Model.Material.Set( "ObjectUniformBuffer", uniformBuffer );
		Model.Draw();
	}
}
