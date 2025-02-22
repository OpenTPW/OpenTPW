using System.Runtime.InteropServices;
using Veldrid;

namespace OpenTPW;

public class Mesh : Resource
{
	public DeviceBuffer VertexBuffer { get; private set; } = null!;
	public DeviceBuffer? IndexBuffer { get; private set; } = null;

	public Material Material { get; private set; }
	public bool IsIndexed { get; private set; }

	private uint indexCount;
	private uint vertexCount;

	public Transform LocalTransform { get; set; } = new();

	public Mesh( Vertex[] vertices, uint[] indices, Material material )
	{
		Material = material;
		IsIndexed = true;

		SetupMesh( vertices, indices );

		All.Add( this );
	}

	public Mesh( Vertex[] vertices, Material material )
	{
		Material = material;
		IsIndexed = false;

		SetupMesh( vertices );

		All.Add( this );
	}

	private void SetupMesh( Vertex[] vertices )
	{
		var factory = Device.ResourceFactory;
		var vertexStructSize = (uint)Marshal.SizeOf( typeof( Vertex ) );
		vertexCount = (uint)vertices.Length;

		VertexBuffer = factory.CreateBuffer(
			new BufferDescription( vertexCount * vertexStructSize, BufferUsage.VertexBuffer )
		);

		Device.UpdateBuffer( VertexBuffer, 0, vertices );
	}

	private void SetupMesh( Vertex[] vertices, uint[] indices )
	{
		SetupMesh( vertices );

		var factory = Device.ResourceFactory;
		indexCount = (uint)indices.Length;

		IndexBuffer = factory.CreateBuffer(
			new BufferDescription( indexCount * sizeof( uint ), BufferUsage.IndexBuffer )
		);

		Device.UpdateBuffer( IndexBuffer, 0, indices );
	}

	internal void Draw()
	{
		var commandList = Render.GetCommandList();

		commandList.SetVertexBuffer( 0, VertexBuffer );
		commandList.SetPipeline( Material.Pipeline );

		Material.GetTemporaryResourceSet( out var resourceSets );

		for ( uint i = 0; i < resourceSets.Length; ++i )
			commandList.SetGraphicsResourceSet( i, resourceSets[i] );

		if ( IsIndexed )
		{
			commandList.SetIndexBuffer( IndexBuffer, IndexFormat.UInt32 );

			commandList.DrawIndexed(
				indexCount: indexCount,
				instanceCount: 1,
				indexStart: 0,
				vertexOffset: 0,
				instanceStart: 0
			);
		}
		else
		{
			commandList.Draw( vertexCount );
		}
	}
}
