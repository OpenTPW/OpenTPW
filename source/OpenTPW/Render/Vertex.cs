using System.Runtime.InteropServices;
using OpenTPW.Graphics;
using SDL;

namespace OpenTPW;

/// <summary>Interleaved model vertex, retaining the original position/normal/UV/index/flag byte layout.</summary>
[StructLayout( LayoutKind.Sequential )]
public struct Vertex
{
	public Vector3 Position { get; set; }
	public Vector3 Normal { get; set; }
	public Vector2 TexCoords { get; set; }
	public int TexIndex { get; set; }
	public uint MatFlags { get; set; }

	/// <summary>MeshInput locations from content/shaders/includes/sdl-gpu.hlsli; packed byte offsets stay unchanged.</summary>
	public static GpuVertexLayout Layout { get; } = new( 40,
		new( 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3, 0 ),
		new( 1, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3, 12 ),
		new( 2, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 24 ),
		new( 3, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_INT, 32 ),
		new( 4, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_UINT, 36 ) );

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
