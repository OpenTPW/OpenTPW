using System.Numerics;

namespace OpenTPW;

public sealed class LobbyIsland : Entity
{
	public LobbyIsland( Vector3 _position, string themeName )
	{
		Position = _position;

		var modelPrefix = themeName[0..3];
		var modelFile = new ModelFile( $"lobby/terrain/{modelPrefix}_isle.md2" );

		foreach ( var mesh in modelFile.Meshes )
		{
			var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
			var textures = new List<Texture>();

			for ( int i = 0; i < 16; ++i )
			{
				if ( mesh.Materials.Length <= i )
				{
					textures.Add( Texture.Missing );
				}
				else
				{
					var j = mesh.Materials[i];
					textures.Add( j.Name == null ? Texture.Missing : new Texture( $"lobby/terrain/textures/{j.Name}.wct", TextureFlags.Repeat ) );
				}
			}

			material.Set( $"Color", [.. textures] );

			// Apply the world transform to the vertices rather than decomposing it: some matrices
			// aren't pure rotations (e.g. l_trunk2 in Jun_isle.md2), and the game uses them as they are
			var world = mesh.WorldTransform;
			var vertices = new List<Vertex>();
			for ( int i = 0; i < mesh.Vertices.Length; ++i )
			{
				var position = System.Numerics.Vector3.Transform( mesh.Vertices[i].Position.GetSystemVector3(), world );
				var normal = System.Numerics.Vector3.TransformNormal( mesh.Normals[i].GetSystemVector3(), world );

				vertices.Add( new Vertex()
				{
					Position = new Vector3( position.X, position.Z, position.Y ),
					Normal = new Vector3( normal.X, normal.Z, normal.Y ).Normal,
					TexCoords = mesh.TexCoords[i],
					TexIndex = (int)mesh.Vertices[i].TextureIndex,
					MatFlags = mesh.Materials[(int)mesh.Vertices[i].TextureIndex].Flags
				} );
			}

			var model = new Model( [.. vertices], mesh.Indices, material );

			_ = new ModelEntity()
			{
				Model = model,
				Rotation = Quaternion.Identity,
				Position = Position + new Vector3( 0, 0, -2.5f ),
			};
		}
	}
}
