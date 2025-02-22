using System.Numerics;

namespace OpenTPW;

public class Model : Resource
{
	private List<MeshHandle> _meshes = [];
	public IEnumerable<MeshHandle> Meshes => _meshes;

	public Model( params Mesh[] meshes )
	{
		_meshes = meshes.Select( MeshHandle.From ).ToList();
	}

	public Model( string path, Material baseMaterial )
	{
		var modelFile = new ModelFile( path );
		var textureDirectory = System.IO.Path.Combine( System.IO.Path.GetDirectoryName( path ), "textures" );

		foreach ( var meshData in modelFile.MeshDataList )
		{
			var material = baseMaterial.Copy();
			var textures = new List<Texture>();

			for ( int i = 0; i < 16; ++i )
			{
				textures.Add( Texture.Missing );

				if ( meshData.Materials.Length > i )
				{
					var j = meshData.Materials[i];
					var texturePath = System.IO.Path.Combine( textureDirectory, $"{j.Name}.wct" );
					textures[i] = new Texture( texturePath, TextureFlags.Wrap );
				}
			}

			material.Set( $"Color", [.. textures] );

			var vertices = new List<Vertex>();
			for ( int i = 0; i < meshData.Vertices.Length; ++i )
			{
				vertices.Add( new Vertex()
				{
					Position = new Vector3( meshData.Vertices[i].Position.X, meshData.Vertices[i].Position.Z, meshData.Vertices[i].Position.Y ),
					Normal = meshData.Normals[i],
					TexCoords = meshData.TexCoords[i],
					TexIndex = (int)meshData.Vertices[i].TextureIndex,
					MatFlags = meshData.Materials[(int)meshData.Vertices[i].TextureIndex].Flags
				} );
			}

			var mesh = new Mesh( [.. vertices], meshData.Indices, material );
			Matrix4x4.Decompose( meshData.TransformMatrix, out var scl, out var rot, out var pos );

			var position = new Vector3( pos.X, pos.Z, pos.Y - 2.5f );
			var rotation = new Rotation( rot.X, rot.Z, rot.Y, -rot.W );
			var scale = new Vector3( scl.X, scl.Z, scl.Y );

			mesh.LocalTransform = new Transform()
			{
				Position = position,
				Rotation = rotation,
				Scale = scale
			};

			var meshHandle = MeshHandle.From( mesh );
			_meshes.Add( meshHandle );
		}
	}

	public void Draw( Transform transform )
	{
		foreach ( var meshHandle in Meshes )
		{
			var mesh = meshHandle.Get();
			if ( mesh == null )
				continue;

			var uniformBuffer = new ObjectUniformBuffer
			{
				g_mModel = mesh.LocalTransform.GetMatrix() * transform.GetMatrix(),
				g_mViewProjection = Graphics.ViewProjectionMatrix,
				g_vLightPos = new Vector3( 100, 100, 100 ),
				g_vLightColor = new Vector3( 0, 0, 0 ),
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
