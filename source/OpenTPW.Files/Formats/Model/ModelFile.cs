using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace OpenTPW;

public partial class ModelFile : BaseFormat
{
	public List<Mesh> Meshes { get; private set; }

	/// <summary>
	/// Every node in the model: first one per mesh (in the same order as <see cref="Meshes"/>),
	/// then the transform-only nodes, which are attachment points such as emitters and paths.
	/// </summary>
	public List<Node> Nodes { get; private set; } = [];

	public ModelFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	public ModelFile( string path )
	{
		ReadFromFile( path );
	}

	public struct Vertex
	{
		public Vector3 Position { get; set; }
		public uint TextureIndex { get; set; }
	}

	public class Mesh
	{
		public string Name { get; set; }
		public uint VertexOffset { get; set; }
		public uint UvOffset { get; set; }
		public uint VertCnt { get; set; }
		public uint MaterialOffset { get; set; }
		public uint FaceOffset { get; set; }
		public uint FaceCount { get; set; }
		public uint VertexCount { get; set; }
		public uint VertexOrderLen { get; set; }
		public uint VertexOrderOffset { get; set; }
		public Vertex[] Vertices { get; set; }
		public uint[] Indices { get; set; }

		/// <summary>
		/// Positions as stored, before <see cref="VertexOrder"/> expands them into <see cref="Vertices"/>.
		/// Vertex animation addresses these.
		/// </summary>
		public Vector3[] RawPositions { get; set; } = [];

		/// <summary>
		/// For each entry in <see cref="Vertices"/>, the index of its position in <see cref="RawPositions"/>.
		/// </summary>
		public ushort[] VertexOrder { get; set; } = [];

		/// <summary>
		/// Texture coordinates as stored. V runs bottom-up relative to the image that
		/// <see cref="TextureFile"/> decodes, so sample at (u, 1 - v).
		/// Retail 0x57cb30 uploads ordinary model UVs directly with texture range/offset only.
		/// </summary>
		public Vector2[] TexCoords { get; set; }

		/// <summary>
		/// Transform relative to the parent node (row vectors, translation in the last row).
		/// Use <see cref="WorldTransform"/> to place the mesh in model space.
		/// </summary>
		public Matrix4x4 TransformMatrix { get; set; }

		/// <summary>
		/// Transform to model space: <see cref="TransformMatrix"/> times the parent's world transform.
		/// </summary>
		public Matrix4x4 WorldTransform { get; set; }

		/// <summary>
		/// Index into <see cref="Nodes"/> of the parent node, or -1 for a root.
		/// </summary>
		public int ParentIndex { get; set; }

		public MaterialData[] Materials { get; set; }

		/// <summary>
		/// One normal per entry of <see cref="Vertices"/>, in node space. Authored normals from the record's
		/// +0x64 table when present (TP.exe 0x570ca0 lights with them; a few are zero and get ambient only),
		/// otherwise smoothed face normals.
		/// </summary>
		public Vector3[] Normals { get; set; }

		/// <summary>Node flags; 0x1000000 marks a prelit mesh (0x577000 skips lighting it).</summary>
		public uint Flags { get; set; }

		/// <summary>
		/// ARGB colour per entry of <see cref="Vertices"/> for prelit meshes (flag 0x1000000, +0x64 table,
		/// used directly by 0x57e2d0); null for meshes lit at run time.
		/// </summary>
		public uint[]? PrelitColours { get; set; }

		internal uint NormalOffset { get; set; }
	}

	public class Node
	{
		public string Name { get; set; } = "";
		public uint Flags { get; set; }
		/// <summary>Authored node identifier, used when the animation instance requests ID binding.</summary>
		public ushort Id { get; set; }

		/// <summary>
		/// Index into <see cref="Nodes"/> of the parent node, or -1 for a root.
		/// </summary>
		public int ParentIndex { get; set; }

		/// <summary>
		/// Transform relative to the parent node (row vectors, translation in the last row).
		/// </summary>
		public Matrix4x4 LocalTransform { get; set; }

		/// <summary>
		/// Transform to model space: <see cref="LocalTransform"/> times the parent's world transform.
		/// </summary>
		public Matrix4x4 WorldTransform { get; set; }

		/// <summary>
		/// The mesh at this node, or null for a transform-only node.
		/// </summary>
		public Mesh? Mesh { get; set; }
	}

	public struct FrameData
	{
		public uint Value;
		public uint MustBeZero;
		public ushort Pad;
		public ushort willBe1;
		public uint FrameNameOff;
	}

	public class MaterialData
	{
		public uint FrameOffset;
		public ushort a;
		public ushort b;
		public ushort StartIndex;
		public ushort EndIndex;
		public uint Pad;

		/// <summary>
		/// Texture name, or null for a material without a texture.
		/// </summary>
		public string Name;
		public uint Flags;
	}

	private const int MeshRecordSize = 160;
	private const int NodeRecordSize = 88;

	private static string ReadString( BinaryReader reader, long offset )
	{
		reader.BaseStream.Seek( offset, SeekOrigin.Begin );
		var name = new StringBuilder();

		for ( char c = reader.ReadChar(); c != '\0'; c = reader.ReadChar() )
			name.Append( c );

		return name.ToString();
	}

	private static Matrix4x4 ReadMatrix( BinaryReader reader )
	{
		var m = new float[16];
		for ( int i = 0; i < 16; i++ )
			m[i] = reader.ReadSingle();

		return new Matrix4x4(
			m[0], m[1], m[2], m[3],
			m[4], m[5], m[6], m[7],
			m[8], m[9], m[10], m[11],
			m[12], m[13], m[14], m[15]
		);
	}

	protected override void ReadFromStream( Stream stream )
	{
		using ( BinaryReader reader = new BinaryReader( stream, Encoding.ASCII, true ) )
		{
			Meshes = new List<Mesh>();
			Nodes = new List<Node>();

			// Animation files (e.g. "<model>m1.md2") are a bare header with no mesh table;
			// their tracks refer to the nodes of the base model.
			reader.BaseStream.Seek( 0x70, SeekOrigin.Begin );
			if ( reader.ReadUInt32() == 0 )
				return;

			reader.BaseStream.Seek( 0x50, SeekOrigin.Begin );
			uint off2 = reader.ReadUInt32();

			reader.BaseStream.Seek( 0x36, SeekOrigin.Begin );
			ushort frameCount = reader.ReadUInt16();

			reader.BaseStream.Seek( off2 + (8 * frameCount), SeekOrigin.Begin );

			List<string> textures = new();
			for ( int i = 0; i < frameCount; i++ )
			{
				var texName = Encoding.ASCII.GetString( reader.ReadBytes( 20 ) );
				textures.Add( texName );
			}

			// Node count covers meshes and transform-only nodes; meshes come first.
			reader.BaseStream.Seek( 0x42, SeekOrigin.Begin );
			ushort nodeCnt = reader.ReadUInt16();
			ushort meshCnt = reader.ReadUInt16();

			reader.BaseStream.Seek( 0x50, SeekOrigin.Begin );
			uint textureListOffset = reader.ReadUInt32();

			reader.BaseStream.Seek( 0x54, SeekOrigin.Begin );
			uint frameListOffset = reader.ReadUInt32();

			reader.BaseStream.Seek( frameListOffset, SeekOrigin.Begin );
			List<FrameData> frameData = new();
			for ( int i = 0; i < frameCount; ++i )
			{
				frameData.Add( new()
				{
					Value = reader.ReadUInt32(),
					MustBeZero = reader.ReadUInt32(),
					Pad = reader.ReadUInt16(),
					willBe1 = reader.ReadUInt16(),
					FrameNameOff = reader.ReadUInt32()
				} );
			}

			reader.BaseStream.Seek( 0x70, SeekOrigin.Begin );
			uint meshPtr = reader.ReadUInt32();
			uint nodePtr = reader.ReadUInt32();

			// Parent links are file offsets of records in either table; turn them into node indices.
			int NodeIndexAt( uint offset )
			{
				if ( offset >= meshPtr && offset < meshPtr + MeshRecordSize * meshCnt && (offset - meshPtr) % MeshRecordSize == 0 )
					return (int)((offset - meshPtr) / MeshRecordSize);

				int transformNodeCnt = nodeCnt - meshCnt;
				if ( offset >= nodePtr && offset < nodePtr + NodeRecordSize * transformNodeCnt && (offset - nodePtr) % NodeRecordSize == 0 )
					return meshCnt + (int)((offset - nodePtr) / NodeRecordSize);

				return -1;
			}

			Meshes = new List<Mesh>( meshCnt );
			Nodes = new List<Node>( nodeCnt );

			for ( int meshIdx = 0; meshIdx < meshCnt; meshIdx++ )
			{
				reader.BaseStream.Seek( meshPtr + (MeshRecordSize * meshIdx), SeekOrigin.Begin );

				uint nodeFlags = reader.ReadUInt32();
				int parentIndex = NodeIndexAt( reader.ReadUInt32() );
				reader.BaseStream.Seek( 8, SeekOrigin.Current ); // Skip sibling and child links

				Matrix4x4 transformMatrix = ReadMatrix( reader );

				uint nodeId = reader.ReadUInt32();
				uint nOff = reader.ReadUInt32();
				ushort vertexCount = reader.ReadUInt16();
				ushort materialCount = reader.ReadUInt16();
				ushort faceCount = reader.ReadUInt16();
				ushort vertexOrderLength = reader.ReadUInt16();
				uint vertexOffset = reader.ReadUInt32();
				// +0x64: per-vertex normals, or ARGB colours when the node flag 0x1000000 is set.
				uint normalOffset = reader.ReadUInt32();
				uint uvOffset = reader.ReadUInt32();
				uint materialOffset = reader.ReadUInt32();
				uint faceOffset = reader.ReadUInt32();
				reader.BaseStream.Seek( 32, SeekOrigin.Current ); // Skip _idk2 to _37
				uint vertexOrderOffset = reader.ReadUInt32();
				reader.BaseStream.Seek( 8, SeekOrigin.Current ); // Skip _38 and _39

				string name = ReadString( reader, nOff );

				reader.BaseStream.Seek( materialOffset, SeekOrigin.Begin );
				List<MaterialData> materials = new();

				for ( int i = 0; i < materialCount; ++i )
				{
					materials.Add( new()
					{
						FrameOffset = reader.ReadUInt32(),
						a = reader.ReadUInt16(),
						b = reader.ReadUInt16(),
						StartIndex = reader.ReadUInt16(),
						EndIndex = reader.ReadUInt16(),
						Pad = reader.ReadUInt32()
					} );
				}

				foreach ( var material in materials )
				{
					// No texture (e.g. "Hoarding" in lobby/terrain/Spa_isle.md2)
					if ( material.FrameOffset == 0 )
						continue;

					// start at texIdOffset, divide by 8 to get index
					uint frameId = (material.FrameOffset - textureListOffset) / 8;
					var frame = frameData[(int)frameId];

					reader.BaseStream.Seek( frame.FrameNameOff, SeekOrigin.Begin );
					var texName = Encoding.ASCII.GetString( reader.ReadBytes( 20 ) );
					var texture = Path.GetFileNameWithoutExtension( texName );

					material.Name = texture;

					reader.BaseStream.Seek( material.FrameOffset, SeekOrigin.Begin );
					material.Flags = reader.ReadUInt32();
				}

				var mesh = new Mesh
				{
					Name = name,
					VertCnt = materialCount,
					UvOffset = uvOffset,
					VertexOffset = vertexOffset,
					MaterialOffset = materialOffset,
					FaceOffset = faceOffset,
					FaceCount = faceCount,
					VertexCount = vertexCount,
					VertexOrderLen = vertexOrderLength,
					VertexOrderOffset = vertexOrderOffset,
					TransformMatrix = transformMatrix,
					ParentIndex = parentIndex,
					Materials = materials.ToArray(),
					Flags = nodeFlags,
					NormalOffset = normalOffset
				};

				Meshes.Add( mesh );
				Nodes.Add( new Node
				{
					Name = name,
					Flags = nodeFlags,
					Id = (ushort)nodeId,
					ParentIndex = parentIndex,
					LocalTransform = transformMatrix,
					Mesh = mesh
				} );
			}

			// Transform-only nodes follow the meshes in node numbering
			for ( int nodeIdx = meshCnt; nodeIdx < nodeCnt; nodeIdx++ )
			{
				reader.BaseStream.Seek( nodePtr + (NodeRecordSize * (nodeIdx - meshCnt)), SeekOrigin.Begin );

				uint nodeFlags = reader.ReadUInt32();
				int parentIndex = NodeIndexAt( reader.ReadUInt32() );
				reader.BaseStream.Seek( 8, SeekOrigin.Current ); // Skip sibling and child links

				Matrix4x4 transformMatrix = ReadMatrix( reader );

				uint nodeId = reader.ReadUInt32();
				uint nameOffset = reader.ReadUInt32();

				Nodes.Add( new Node
				{
					Name = ReadString( reader, nameOffset ),
					Flags = nodeFlags,
					Id = (ushort)nodeId,
					ParentIndex = parentIndex,
					LocalTransform = transformMatrix
				} );
			}

			// Parents can come after their children, so resolve world transforms recursively
			var resolved = new bool[Nodes.Count];

			Matrix4x4 ResolveWorld( int nodeIdx, int depth )
			{
				var node = Nodes[nodeIdx];
				if ( resolved[nodeIdx] )
					return node.WorldTransform;

				var world = node.LocalTransform;
				if ( node.ParentIndex >= 0 && depth < Nodes.Count )
					world *= ResolveWorld( node.ParentIndex, depth + 1 );

				node.WorldTransform = world;
				resolved[nodeIdx] = true;
				return world;
			}

			for ( int nodeIdx = 0; nodeIdx < Nodes.Count; nodeIdx++ )
			{
				var world = ResolveWorld( nodeIdx, 0 );
				if ( Nodes[nodeIdx].Mesh is { } mesh )
					mesh.WorldTransform = world;
			}

			// Process mesh data
			for ( int meshIdx = 0; meshIdx < Meshes.Count; meshIdx++ )
			{
				Mesh mesh = Meshes[meshIdx];
				uint meshPosEnd = (meshIdx + 1 < Meshes.Count) ? Meshes[meshIdx + 1].VertexOffset : Meshes[0].MaterialOffset;
				uint cnt = (meshPosEnd - mesh.VertexOffset) / (3 * 4 * 4);

				reader.BaseStream.Seek( mesh.UvOffset, SeekOrigin.Begin );
				List<Vector2> uvs = new List<Vector2>();
				uint uvCnt = mesh.VertexOrderLen;
				if ( uvCnt % 4 != 0 )
				{
					uvCnt += (uint)(4 - (uvCnt % 4));
				}

				while ( uvCnt > 0 )
				{
					int elem = (int)Math.Min( uvCnt, 4 );
					Vector2[] points = new Vector2[elem];

					for ( int i = 0; i < elem; i++ )
						points[i].X = reader.ReadSingle();
					for ( int i = 0; i < elem; i++ )
						points[i].Y = reader.ReadSingle();

					uvs.AddRange( points );
					uvCnt -= (uint)elem;
				}

				mesh.TexCoords = uvs.ToArray();

				reader.BaseStream.Seek( mesh.VertexOffset, SeekOrigin.Begin );

				List<Vector3> vertices = new List<Vector3>();
				uint c = mesh.VertexCount;
				if ( c % 4 != 0 )
				{
					c += (uint)(4 - (c % 4));
				}

				while ( c > 0 )
				{
					int elem = (int)Math.Min( c, 4 );
					Vector3[] points = new Vector3[elem];

					for ( int i = 0; i < elem; i++ )
						points[i].X = reader.ReadSingle();
					for ( int i = 0; i < elem; i++ )
						points[i].Y = reader.ReadSingle();
					for ( int i = 0; i < elem; i++ )
						points[i].Z = reader.ReadSingle();

					vertices.AddRange( points );
					c -= (uint)elem;
				}

				// Read vertex order
				reader.BaseStream.Seek( mesh.VertexOrderOffset, SeekOrigin.Begin );
				ushort[] vertexOrder = new ushort[mesh.VertexOrderLen];
				for ( int i = 0; i < mesh.VertexOrderLen; i++ )
				{
					vertexOrder[i] = reader.ReadUInt16();
				}

				// Re-order vertices
				Vertex[] reorderedVertices = new Vertex[vertexOrder.Length];
				for ( int i = 0; i < vertexOrder.Length; i++ )
				{
					int textureIndex = 0;
					for ( int j = 0; j < mesh.Materials.Length; ++j )
					{
						if ( i >= mesh.Materials[j].StartIndex && i <= mesh.Materials[j].EndIndex )
						{
							textureIndex = j;
							break;
						}
					}

					var vertex = new Vertex()
					{
						Position = vertices[vertexOrder[i]],
						TextureIndex = (uint)textureIndex
					};

					reorderedVertices[i] = vertex;
				}

				mesh.Vertices = reorderedVertices;
				mesh.RawPositions = vertices.ToArray();
				mesh.VertexOrder = vertexOrder;

				// Parse face data
				reader.BaseStream.Seek( mesh.FaceOffset, SeekOrigin.Begin );
				List<uint> indices = new List<uint>();

				for ( int i = 0; i < mesh.FaceCount; i++ )
				{
					reader.ReadUInt16(); // Skip _ptr
					ushort _a = reader.ReadUInt16();
					ushort _b = reader.ReadUInt16();
					ushort _c = reader.ReadUInt16();

					// Reverse winding order
					indices.Add( (uint)_a );
					indices.Add( (uint)_b );
					indices.Add( (uint)_c );
				}

				mesh.Indices = indices.ToArray();

				CalculateNormals( mesh );
				ReadVertexLighting( reader, mesh );
			}
		}
	}

	/// <summary>
	/// The +0x64 table holds one entry per vertex-order entry: float3 normals (0x570ca0 steps 12 bytes and
	/// passes them to 0x5707f0), or u32 colours for prelit meshes.
	/// </summary>
	private static void ReadVertexLighting( BinaryReader reader, Mesh mesh )
	{
		if ( mesh.NormalOffset == 0 || mesh.VertexOrderLen == 0 ) return;
		reader.BaseStream.Seek( mesh.NormalOffset, SeekOrigin.Begin );
		if ( (mesh.Flags & 0x1000000) != 0 )
		{
			mesh.PrelitColours = new uint[mesh.VertexOrderLen];
			for ( int i = 0; i < mesh.PrelitColours.Length; i++ ) mesh.PrelitColours[i] = reader.ReadUInt32();
			return;
		}
		var normals = new Vector3[mesh.VertexOrderLen];
		for ( int i = 0; i < normals.Length; i++ ) normals[i] = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		mesh.Normals = normals;
	}

	private void CalculateNormals( Mesh mesh )
	{
		Vector3[] normals = new Vector3[mesh.Vertices.Length];

		// Initialize all normals to zero
		for ( int i = 0; i < normals.Length; i++ )
		{
			normals[i] = Vector3.Zero;
		}

		// Calculate face normals and accumulate them for each vertex
		for ( int i = 0; i < mesh.Indices.Length; i += 3 )
		{
			uint i1 = mesh.Indices[i];
			uint i2 = mesh.Indices[i + 1];
			uint i3 = mesh.Indices[i + 2];

			Vector3 v1 = mesh.Vertices[i1].Position;
			Vector3 v2 = mesh.Vertices[i2].Position;
			Vector3 v3 = mesh.Vertices[i3].Position;

			Vector3 edge1 = v2 - v1;
			Vector3 edge2 = v3 - v1;

			Vector3 faceNormal = Vector3.Cross( edge1, edge2 );
			faceNormal = faceNormal.Normal; // Ensure face normal is unit length

			// Accumulate the face normal to all three vertices
			normals[i1] += faceNormal;
			normals[i2] += faceNormal;
			normals[i3] += faceNormal;
		}

		// Normalize all vertex normals to average them
		for ( int i = 0; i < normals.Length; i++ )
		{
			if ( normals[i] != Vector3.Zero )
				normals[i] = normals[i].Normal;
		}

		mesh.Normals = normals;
	}
}
