using System.Numerics;
using System.Text;

namespace OpenTPW;

public partial class ModelFile : BaseFormat
{
	public List<MeshData> MeshDataList { get; private set; }

	public ModelFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	public ModelFile( string path )
	{
		ReadFromFile( path );
	}

	public struct VertexData
	{
		public Vector3 Position { get; set; }
		public uint TextureIndex { get; set; }
	}

	public class MeshData
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
		public VertexData[] Vertices { get; set; }
		public uint[] Indices { get; set; }
		public Vector2[] TexCoords { get; set; }
		public Matrix4x4 TransformMatrix { get; set; }
		public MaterialData[] Materials { get; set; }

		public Vector3[] Normals { get; set; }
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

		public string Name;
		public uint Flags;
	}

	protected override void ReadFromStream( Stream stream )
	{
		using ( BinaryReader reader = new BinaryReader( stream, Encoding.ASCII, true ) )
		{
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

			reader.BaseStream.Seek( 0x44, SeekOrigin.Begin );
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

			reader.BaseStream.Seek( meshPtr, SeekOrigin.Begin );

			MeshDataList = new List<MeshData>( meshCnt );

			for ( int meshIdx = 0; meshIdx < meshCnt; meshIdx++ )
			{
				var meshStart = meshPtr + (160 * meshIdx);
				reader.BaseStream.Seek( meshStart, SeekOrigin.Begin );

				var flags = reader.ReadUInt32();
				_ = reader.ReadUInt32();
				_ = reader.ReadUInt32();
				var childPtr = reader.ReadUInt32();

				// Read mat4
				Matrix4x4 transformMatrix;

				{
					var ma = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
					var mb = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
					var mc = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
					var md = new Vector4( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
					transformMatrix = new Matrix4x4(
						ma.X, ma.Y, ma.Z, ma.W,
						mb.X, mb.Y, mb.Z, mb.W,
						mc.X, mc.Y, mc.Z, mc.W,
						md.X, md.Y, md.Z, md.W
					);
				}

				uint texIndex = reader.ReadUInt32();
				uint nOff = reader.ReadUInt32();
				ushort vertexCount = reader.ReadUInt16();
				ushort materialCount = reader.ReadUInt16();
				ushort faceCount = reader.ReadUInt16();
				ushort vertexOrderLength = reader.ReadUInt16();
				uint vertexOffset = reader.ReadUInt32();
				uint unkOffset = reader.ReadUInt32();
				uint uvOffset = reader.ReadUInt32();
				uint materialOffset = reader.ReadUInt32();
				uint faceOffset = reader.ReadUInt32();

				_ = reader.ReadUInt32(); // Always 0

				// bbox
				var mins = new Vector3(
					reader.ReadSingle(),
					reader.ReadSingle(),
					reader.ReadSingle()
				);

				var maxs = new Vector3(
					reader.ReadSingle(),
					reader.ReadSingle(),
					reader.ReadSingle()
				);

				// Log.Info( $"BBox min: {mins}, max: {maxs}" );

				_ = reader.ReadUInt32(); // usually 0
				uint vertexOrderOffset = reader.ReadUInt32();
				_ = reader.ReadUInt32(); // usually 0
				_ = reader.ReadUInt32(); // usually 0

				reader.BaseStream.Seek( nOff, SeekOrigin.Begin );
				string name = "";
				char c;

				do
				{
					c = reader.ReadChar();
					name += c;
				} while ( c != '\0' );

				Log.Info( $"{name} (model offset {meshPtr}, mesh offset {meshStart}):\n- Flags: {flags}\n- Child ptr: {childPtr}" );

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
					// start at texIdOffset, divide by 8 to get index
					uint frameId = (material.FrameOffset - textureListOffset) / 8;

					if ( frameId > frameData.Count )
						continue;

					var frame = frameData[(int)frameId];

					reader.BaseStream.Seek( frame.FrameNameOff, SeekOrigin.Begin );
					var texName = Encoding.ASCII.GetString( reader.ReadBytes( 20 ) );
					var texture = Path.GetFileNameWithoutExtension( texName );

					material.Name = texture;

					reader.BaseStream.Seek( material.FrameOffset, SeekOrigin.Begin );
					material.Flags = reader.ReadUInt32();
				}

				MeshDataList.Add( new MeshData
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
					Materials = materials.ToArray()
				} );
			}

			// Process mesh data
			for ( int meshIdx = 0; meshIdx < MeshDataList.Count; meshIdx++ )
			{
				MeshData mesh = MeshDataList[meshIdx];
				uint meshPosEnd = (meshIdx + 1 < MeshDataList.Count) ? MeshDataList[meshIdx + 1].VertexOffset : MeshDataList[0].MaterialOffset;
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
				VertexData[] reorderedVertices = new VertexData[vertexOrder.Length];
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

					var vertex = new VertexData()
					{
						Position = vertices[vertexOrder[i]],
						TextureIndex = (uint)textureIndex
					};

					reorderedVertices[i] = vertex;
				}

				mesh.Vertices = reorderedVertices;

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
			}
		}
	}

	private void CalculateNormals( MeshData mesh )
	{
		Vector3[] normals = new Vector3[mesh.Vertices.Length];

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
			if ( faceNormal.LengthSquared > 0 )
			{
				faceNormal = faceNormal.Normal;

				normals[i1] += faceNormal;
				normals[i2] += faceNormal;
				normals[i3] += faceNormal;
			}
		}

		// Normalize all accumulated normals
		for ( int i = 0; i < normals.Length; i++ )
		{
			if ( normals[i] != Vector3.Zero )
			{
				normals[i] = normals[i].Normal;
			}
		}

		mesh.Normals = normals;
	}
}
