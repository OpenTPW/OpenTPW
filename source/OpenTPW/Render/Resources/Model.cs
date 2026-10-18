using System.Numerics;

namespace OpenTPW;

public partial class Model : Resource
{
	private readonly List<MeshHandle> _meshes = [];

	public IEnumerable<MeshHandle> Meshes => _meshes;

	public Model( params Mesh[] meshes )
	{
		_meshes = meshes.Select( MeshHandle.From ).ToList();
	}

	public Model( string path, Material baseMaterial, Func<string, Texture?>? textureOverride = null )
	{
		var modelFile = new ModelFile( path );
		Animation = new ModelAnimationPlayer( modelFile );
		var materials = new ModelMaterialBuilder( path, baseMaterial, TextureFlags.Wrap, textureOverride, warnOnMissing: true );
		for ( int meshIndex = 0; meshIndex < modelFile.MeshDataList.Count; meshIndex++ )
		{
			var meshData = modelFile.MeshDataList[meshIndex];
			int nodeIndex = modelFile.Nodes.FindIndex( node => node.Offset == meshData.NodeOffset );
			foreach ( var batch in ModelGeometry.CreateBatches( meshData ) )
			{
				var material = materials.Create( batch.Materials );
				var mesh = new Mesh( batch.Vertices, batch.Indices, material )
				{
					LocalMatrix = ModelGeometry.ToEngineMatrix( meshData.TransformMatrix )
				};
				_meshes.Add( MeshHandle.From( mesh ) );
				_animationMeshes.Add( new AnimatedMesh( mesh, meshData, batch, nodeIndex, meshIndex, new Vector3[meshData.Vertices.Length] ) );
			}
		}

		DiscoverAnimations( path );
		if ( modelFile.Animation != null )
		{
			_clips["default"] = modelFile.Animation;
			PlayAnimation( "default" );
		}
		else if ( _animationPaths.ContainsKey( "i" ) )
		{
			PlayAnimation( "i" );
		}
	}

	public void Draw( Transform transform )
	{
		Draw( transform.GetMatrix(), new Vector3( 100, 100, 100 ), Vector3.Zero );
	}

	internal void Draw( Matrix4x4 modelMatrix, Vector3 lightPosition, Vector3 lightColor )
	{
		PrepareAnimation();
		foreach ( var meshHandle in Meshes )
		{
			var mesh = meshHandle.Get();
			if ( mesh == null )
			{
				continue;
			}

			var uniformBuffer = new ObjectUniformBuffer
			{
				g_mModel = mesh.GetLocalMatrix() * modelMatrix,
				g_mViewProjection = Graphics.ViewProjectionMatrix,
				g_vLightPos = lightPosition,
				g_vLightColor = lightColor,
				g_vCameraPos = Camera.Position,
				g_flTime = Time.Now,
				_padding0 = 0,
				_padding1 = 0,
				_padding2 = 0,
			};
			using var ctx = new ResourceContext();
			ctx.Set( "ObjectUniformBuffer", uniformBuffer );
			mesh.Draw();
		}
	}
}
