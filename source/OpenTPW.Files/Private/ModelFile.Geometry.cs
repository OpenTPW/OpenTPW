using System.Numerics;

namespace OpenTPW;

public partial class ModelFile
{
	private void ReadGeometry()
	{
		ushort frameCount = U16( 0x36 );
		ushort meshCount = U16( 0x44 );
		ushort nodeCount = U16( 0x42 );
		if ( nodeCount < meshCount )
		{
			throw new InvalidDataException( "MD2 node count is smaller than its mesh count." );
		}

		uint frameRefs = U32( 0x50 );
		uint frameTable = U32( 0x54 );
		uint meshTable = U32( 0x70 );
		uint secondaryTable = U32( 0x74 );
		RequireSection( frameRefs, (ulong)frameCount * 8, "frame references" );
		RequireSection( frameTable, (ulong)frameCount * 0x10, "frame table" );
		RequireSection( meshTable, (ulong)meshCount * MeshRecordSize, "mesh table" );
		RequireSection( secondaryTable, (ulong)(nodeCount - meshCount) * 0x58, "secondary nodes" );
		for ( int i = 0; i < nodeCount; i++ )
		{
			uint recordOffset = i < meshCount ? meshTable + (uint)i * MeshRecordSize : secondaryTable + (uint)(i - meshCount) * 0x58;
			Nodes.Add( new NodeData
			{
				Id = U16( recordOffset + 0x50 ),
				Flags = U32( recordOffset ),
				Offset = recordOffset,
				ParentOffset = U32( recordOffset + 4 ),
				Name = U32( recordOffset + 0x54 ) == 0 ? "" : CString( U32( recordOffset + 0x54 ) ),
				LocalMatrix = Matrix( recordOffset + 0x10 )
			} );
		}

		for ( int i = 0; i < frameCount; i++ )
		{
			uint recordOffset = frameTable + (uint)i * 0x10;
			uint nameOffset = U32( recordOffset + 0x0C );
			Frames.Add( new FrameData
			{
				Value = U32( recordOffset ),
				MustBeZero = U32( recordOffset + 4 ),
				Pad = U16( recordOffset + 8 ),
				willBe1 = U16( recordOffset + 0x0A ),
				FrameNameOff = nameOffset,
				Name = nameOffset == 0 ? "" : FixedString( nameOffset, 20 )
			} );
		}

		for ( int i = 0; i < meshCount; i++ )
		{
			MeshDataList.Add( ReadMesh( meshTable + (uint)i * MeshRecordSize, i, frameRefs, frameCount ) );
		}

		BuildHierarchy();
	}

	private MeshData ReadMesh( uint recordOffset, int meshIndex, uint frameRefs, ushort frameCount )
	{
		ushort positionCount = U16( recordOffset + 0x58 );
		ushort materialCount = U16( recordOffset + 0x5A );
		ushort faceCount = U16( recordOffset + 0x5C );
		ushort cornerCount = U16( recordOffset + 0x5E );
		uint positionOffset = U32( recordOffset + 0x60 );
		uint uvOffset = U32( recordOffset + 0x68 );
		uint materialOffset = U32( recordOffset + 0x6C );
		uint faceOffset = U32( recordOffset + 0x70 );
		uint orderOffset = U32( recordOffset + 0x94 );
		// Both arrays are structure-of-arrays in groups of four. Do not
		// infer their size from the next mesh or material section.
		RequireSection( positionOffset, (ulong)((positionCount + 3) / 4) * 48, "positions" );
		RequireSection( uvOffset, (ulong)((cornerCount + 3) / 4) * 32, "UVs" );
		RequireSection( materialOffset, (ulong)materialCount * 16, "materials" );
		RequireSection( faceOffset, (ulong)faceCount * 8, "faces" );
		RequireSection( orderOffset, (ulong)cornerCount * 2, "vertex order" );
		var mesh = new MeshData
		{
			NodeOffset = recordOffset,
			Name = U32( recordOffset + 0x54 ) == 0 ? "" : CString( U32( recordOffset + 0x54 ) ),
			ParentPtr = U32( recordOffset + 4 ),
			ChildPtr = U32( recordOffset + 0x0C ),
			TransformMatrix = Matrix( recordOffset + 0x10 ),
			VertCnt = positionCount,
			VertexCount = positionCount,
			FaceCount = faceCount,
			VertexOrderLen = cornerCount,
			VertexOffset = positionOffset,
			UvOffset = uvOffset,
			MaterialOffset = materialOffset,
			FaceOffset = faceOffset,
			VertexOrderOffset = orderOffset,
			Positions = ReadPositions( positionOffset, positionCount ),
			TexCoords = ReadUvs( uvOffset, cornerCount )
		};
		ReadMaterials( mesh, meshIndex, materialCount, frameRefs, frameCount );
		ReadVertices( mesh, meshIndex, cornerCount, positionCount );
		ReadFaces( mesh, meshIndex, faceCount, cornerCount );
		mesh.Normals = CalculateNormals( mesh );
		return mesh;
	}

	private void ReadMaterials( MeshData mesh, int meshIndex, ushort materialCount, uint frameRefs, ushort frameCount )
	{
		mesh.Materials = new MaterialData[materialCount];
		for ( int j = 0; j < materialCount; j++ )
		{
			uint materialRecord = mesh.MaterialOffset + (uint)j * 16;
			uint frameRef = U32( materialRecord );
			if ( frameRef != 0 &&
				(frameRef < frameRefs || (ulong)frameRef >= (ulong)frameRefs + (ulong)frameCount * 8 || (frameRef - frameRefs) % 8 != 0) )
			{
				throw new InvalidDataException( $"Mesh {meshIndex} material {j}: invalid frame reference." );
			}

			int frameIndex = frameRef == 0 ? -1 : checked((int)((frameRef - frameRefs) / 8));
			ushort firstFace = U16( materialRecord + 4 ), batchFaceCount = U16( materialRecord + 6 );
			if ( (uint)firstFace + batchFaceCount > mesh.FaceCount )
			{
				throw new InvalidDataException( $"Mesh {meshIndex} material {j}: invalid face range." );
			}

			ushort start = U16( materialRecord + 8 ), end = U16( materialRecord + 0x0A );
			if ( start > end || end >= mesh.VertexOrderLen )
			{
				throw new InvalidDataException( $"Mesh {meshIndex} material {j}: invalid corner range." );
			}

			mesh.Materials[j] = new MaterialData
			{
				FrameOffset = frameRef,
				FirstFace = firstFace,
				FaceCount = batchFaceCount,
				StartIndex = start,
				EndIndex = end,
				Pad = U32( materialRecord + 0x0C ),
				Name = frameIndex < 0 ? "" : Path.GetFileNameWithoutExtension( Frames[frameIndex].Name ),
				Flags = frameRef == 0 ? 0 : U32( frameRef )
			};
		}
	}

	private void ReadVertices( MeshData mesh, int meshIndex, ushort cornerCount, ushort positionCount )
	{
		var order = new ushort[cornerCount];
		for ( int j = 0; j < cornerCount; j++ )
		{
			order[j] = U16( mesh.VertexOrderOffset + (uint)j * 2 );
			if ( order[j] >= positionCount )
			{
				throw new InvalidDataException( $"Mesh {meshIndex}: vertex order index out of range." );
			}
		}

		mesh.Vertices = new VertexData[cornerCount];
		for ( int j = 0; j < cornerCount; j++ )
		{
			mesh.Vertices[j] = new VertexData
			{
				Position = mesh.Positions[order[j]],
				PositionIndex = order[j]
			};
		}
	}

	private void ReadFaces( MeshData mesh, int meshIndex, ushort faceCount, ushort cornerCount )
	{
		mesh.Indices = new uint[faceCount * 3];
		mesh.FaceMetadata = new ushort[faceCount];
		for ( int j = 0; j < faceCount; j++ )
		{
			uint faceRecord = mesh.FaceOffset + (uint)j * 8;
			mesh.FaceMetadata[j] = U16( faceRecord );
			// Not a fourth vertex index.
			for ( int k = 0; k < 3; k++ )
			{
				ushort corner = U16( faceRecord + 2u + (uint)k * 2 );
				if ( corner >= cornerCount )
				{
					throw new InvalidDataException( $"Mesh {meshIndex} face {j}: corner index out of range." );
				}

				mesh.Indices[j * 3 + k] = corner;
			}
		}
	}

	private Vector3[] ReadPositions( uint offset, int count )
	{
		var result = new Vector3[count];
		for ( int i = 0; i < count; i++ )
		{
			uint g = offset + (uint)(i / 4) * 48, lane = (uint)(i % 4) * 4;
			result[i] = new Vector3( F32( g + lane ), F32( g + 16 + lane ), F32( g + 32 + lane ) );
		}

		return result;
	}

	private Vector2[] ReadUvs( uint offset, int count )
	{
		var result = new Vector2[count];
		for ( int i = 0; i < count; i++ )
		{
			uint g = offset + (uint)(i / 4) * 32, lane = (uint)(i % 4) * 4;
			result[i] = new Vector2( F32( g + lane ), F32( g + 16 + lane ) );
		}

		return result;
	}

	private void BuildHierarchy()
	{
		var byOffset = new Dictionary<uint, int>( Nodes.Count );
		for ( int i = 0; i < Nodes.Count; i++ )
		{
			if ( !byOffset.TryAdd( Nodes[i].Offset, i ) )
			{
				throw new InvalidDataException( "Overlapping MD2 node tables." );
			}
		}

		var state = new byte[Nodes.Count];
		Matrix4x4 World( int i )
		{
			if ( state[i] == 1 )
			{
				throw new InvalidDataException( "Cyclic mesh hierarchy." );
			}

			if ( state[i] == 2 )
			{
				return Nodes[i].WorldMatrix;
			}

			state[i] = 1;
			var node = Nodes[i];
			node.WorldMatrix = node.LocalMatrix;
			if ( node.ParentOffset != 0 )
			{
				if ( !byOffset.TryGetValue( node.ParentOffset, out int parent ) )
				{
					throw new InvalidDataException( $"Node {node.Name}: invalid parent offset 0x{node.ParentOffset:X}." );
				}

				node.WorldMatrix *= World( parent );
			}

			state[i] = 2;
			return node.WorldMatrix;
		}

		for ( int i = 0; i < Nodes.Count; i++ )
		{
			World( i );
		}

		foreach ( var mesh in MeshDataList )
		{
			mesh.TransformMatrix = Nodes[byOffset[mesh.NodeOffset]].WorldMatrix;
		}
	}

	private static Vector3[] CalculateNormals( MeshData mesh )
	{
		var normals = new Vector3[mesh.Vertices.Length];
		for ( int i = 0; i < mesh.Indices.Length; i += 3 )
		{
			int a = (int)mesh.Indices[i], b = (int)mesh.Indices[i + 1], c = (int)mesh.Indices[i + 2];
			Vector3 p = mesh.Vertices[a].Position, q = mesh.Vertices[b].Position, r = mesh.Vertices[c].Position;
			Vector3 n = Vector3.Cross( q - p, r - p );
			if ( n.LengthSquared == 0 )
			{
				continue;
			}

			n = n.Normal;
			normals[a] += n;
			normals[b] += n;
			normals[c] += n;
		}

		for ( int i = 0; i < normals.Length; i++ )
		{
			if ( normals[i].LengthSquared > 0 )
			{
				normals[i] = normals[i].Normal;
			}
		}

		return normals;
	}
}
