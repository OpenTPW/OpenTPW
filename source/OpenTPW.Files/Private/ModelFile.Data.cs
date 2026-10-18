using System.Numerics;

namespace OpenTPW;

public partial class ModelFile
{
	public struct VertexData
	{
		public Vector3 Position { get; set; }
		public ushort PositionIndex { get; set; }
	}

	public class MeshData
	{
		public uint NodeOffset { get; set; }
		public string Name { get; set; } = "";
		public uint VertexOffset { get; set; }
		public uint UvOffset { get; set; }
		public uint VertCnt { get; set; }

		// Kept for callers; equals VertexCount.
		public uint MaterialOffset { get; set; }
		public uint FaceOffset { get; set; }
		public uint FaceCount { get; set; }
		public uint VertexCount { get; set; }
		public uint VertexOrderLen { get; set; }
		public uint VertexOrderOffset { get; set; }
		public VertexData[] Vertices { get; set; } = Array.Empty<VertexData>();
		public Vector3[] Positions { get; set; } = Array.Empty<Vector3>();
		public uint[] Indices { get; set; } = Array.Empty<uint>();
		public ushort[] FaceMetadata { get; set; } = Array.Empty<ushort>();
		public Vector2[] TexCoords { get; set; } = Array.Empty<Vector2>();
		public Matrix4x4 TransformMatrix { get; set; }
		public MaterialData[] Materials { get; set; } = Array.Empty<MaterialData>();
		public Vector3[] Normals { get; set; } = Array.Empty<Vector3>();
		public uint ParentPtr { get; set; }
		public uint ChildPtr { get; set; }
	}

	// Both primary meshes and secondary transform-only records participate
	// in the same hierarchy. Keep local transforms for future animation.
	public class NodeData
	{
		private const uint HiddenFlag = 0x10;
		public ushort Id { get; init; }
		public uint Flags { get; init; }
		public bool InitiallyVisible => (Flags & HiddenFlag) == 0;
		public uint Offset { get; init; }
		public uint ParentOffset { get; init; }
		public string Name { get; init; } = "";
		public Matrix4x4 LocalMatrix { get; init; }
		public Matrix4x4 WorldMatrix { get; internal set; }
	}

	public struct FrameData
	{
		public uint Value;
		public uint MustBeZero;

		// Cleared by the game's loader; raw file value here.
		public ushort Pad;
		public ushort willBe1;

		// Only set to 1 by a particular loader option.
		public uint FrameNameOff;
		public string Name;
	}

	public class MaterialData
	{
		public uint FrameOffset;
		public ushort FirstFace;
		public ushort FaceCount;

		public bool HasTextureReference => FrameOffset != 0;

		public ushort StartIndex;
		public ushort EndIndex;
		public uint Pad;
		public string Name = "";
		public uint Flags;
		// First dword of the 8-byte frame-reference entry.
	}
}
