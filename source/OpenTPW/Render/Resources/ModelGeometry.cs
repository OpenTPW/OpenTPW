using System.Numerics;

namespace OpenTPW;

/// <summary>Converts MD2 face batches into bounded shader texture slots, without GPU resources.</summary>
public static class ModelGeometry
{
	// The model shaders expose Color0 through Color15. This is a per-draw
	// limit, not a limit on the number of materials in a file mesh.
	public const int TextureSlotCount = 16;

	public sealed record Batch( Vertex[] Vertices, uint[] Indices, ModelFile.MaterialData[] Materials )
	{
		public uint[] SourceCorners { get; init; } = [];
	}

	public static Matrix4x4 ToEngineMatrix( Matrix4x4 m ) => new( m.M11, m.M13, m.M12, m.M14, m.M31, m.M33, m.M32, m.M34, m.M21, m.M23, m.M22, m.M24, m.M41, m.M43, m.M42, m.M44 );

	public static Vector3 ToEngineVector( Vector3 value ) => new( value.X, value.Z, value.Y );

	public static IEnumerable<Batch> CreateBatches( ModelFile.MeshData mesh )
	{
		foreach ( var materials in mesh.Materials.Where( m => m.FaceCount > 0 ).Chunk( TextureSlotCount ) )
		{
			var vertices = new List<Vertex>();
			var indices = new List<uint>();
			var corners = new List<uint>();
			var remap = new Dictionary<(uint Corner, int Slot), uint>();
			for ( int slot = 0; slot < materials.Length; slot++ )
			{
				var material = materials[slot];
				int start = material.FirstFace * 3;
				int end = start + material.FaceCount * 3;
				if ( end > mesh.Indices.Length )
				{
					throw new InvalidDataException( "Material face range exceeds mesh indices." );
				}

				for ( int i = start; i < end; i++ )
				{
					uint corner = mesh.Indices[i];
					// A corner may appear in two material batches. Its position/UV
					// is shared in the file, but its GPU texture slot must differ.
					if ( !remap.TryGetValue( (corner, slot), out uint index ) )
					{
						var position = mesh.Vertices[corner].Position;
						var normal = mesh.Normals[corner];
						index = (uint)vertices.Count;
						vertices.Add( new Vertex
						{
							Position = ToEngineVector( position ),
							Normal = ToEngineVector( normal ),
							TexCoords = mesh.TexCoords[corner],
							TexIndex = slot,
							MatFlags = material.Flags
						} );
						remap.Add( (corner, slot), index );
						corners.Add( corner );
					}

					indices.Add( index );
				}
			}

			yield return new Batch( vertices.ToArray(), indices.ToArray(), materials )
			{
				SourceCorners = corners.ToArray()
			};
		}
	}

	public static void UpdateDeformedVertices( ModelFile.MeshData mesh, Batch batch, Vector3[] positions, Vector3[] normals )
	{
		Array.Clear( normals );
		for ( int i = 0; i < mesh.Indices.Length; i += 3 )
		{
			uint a = mesh.Indices[i], b = mesh.Indices[i + 1], c = mesh.Indices[i + 2];
			var p = positions[mesh.Vertices[a].PositionIndex];
			var q = positions[mesh.Vertices[b].PositionIndex];
			var r = positions[mesh.Vertices[c].PositionIndex];
			var n = Vector3.Cross( q - p, r - p );
			if ( n.LengthSquared > 1e-20f )
			{
				n = n.Normal;
			}

			normals[a] += n;
			normals[b] += n;
			normals[c] += n;
		}

		for ( int i = 0; i < batch.Vertices.Length; i++ )
		{
			uint corner = batch.SourceCorners[i];
			var p = positions[mesh.Vertices[corner].PositionIndex];
			var n = normals[corner];
			if ( n.LengthSquared > 1e-20f )
			{
				n = n.Normal;
			}

			batch.Vertices[i].Position = ToEngineVector( p );
			batch.Vertices[i].Normal = ToEngineVector( n );
		}
	}
}
