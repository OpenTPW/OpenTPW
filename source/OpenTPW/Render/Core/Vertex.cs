using System.Runtime.InteropServices;
using Veldrid;

namespace OpenTPW;

[StructLayout( LayoutKind.Sequential )]
public struct Vertex
{
	public Vector3 Position { get; set; }
	public Vector3 Normal { get; set; }
	public Vector2 TexCoords { get; set; }
	public int TexIndex { get; set; }
	public uint MatFlags { get; set; }

	public static VertexElementDescription[] VertexElementDescriptions = new[]
	{
		new VertexElementDescription( "vPosition", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3 ),
		new VertexElementDescription( "vNormal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3 ),
		new VertexElementDescription( "vTexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2 ),
		new VertexElementDescription( "iTexIndex", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Int1 ),
		new VertexElementDescription( "iMatFlags", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1 ),
	};

	public Vertex()
	{
		
	}

	public Vertex( Vector3 position, Vector3 normal, Vector2 texCoords )
	{
		Position = position;
		Normal = normal;
		TexCoords = texCoords;
	}

	public Vertex( Vector3 position, Vector2 texCoords ) : this()
	{
		Position = position;
		TexCoords = texCoords;
	}

	public Vertex( Vector3 position ) : this()
	{
		Position = position;
	}
}
