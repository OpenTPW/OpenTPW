namespace OpenTPW;

public partial class ModelEntity : Thing
{
	public Model? Model { get; set; }

	protected override void OnRender()
	{
		if ( Model == null )
			return;

		foreach ( var meshHandle in Model.Meshes )
		{
			var mesh = meshHandle.Get();
			if ( mesh == null )
				continue;

			var uniformBuffer = new ObjectUniformBuffer
			{
				g_mModel = mesh.LocalTransform.GetMatrix() * ModelMatrix,
				g_mViewProjection = Graphics.ViewProjectionMatrix,
				g_vLightPos = Level.SunLight?.Position ?? Vector3.Zero,
				g_vLightColor = Level.SunLight?.Color ?? Vector3.One,
				g_vCameraPos = Camera.Position,
				g_flTime = Time.Now,

				_padding0 = 0,
				_padding1 = 0,
				_padding2 = 0,
			};

			using var ctx = new ResourceContext();
			ctx.Set( "ObjectUniformBuffer", uniformBuffer );

			mesh?.Draw();
		}
	}
}
