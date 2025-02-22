namespace OpenTPW;

public partial class StaticModelActor : Actor
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
				g_vLightPos = new Vector3( 0.25f, 0, 1 ),
				g_vLightColor = new Vector3( 1, 1, 1 ),
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
