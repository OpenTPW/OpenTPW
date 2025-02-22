using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace OpenTPW;

internal static partial class Graphics
{
	private const int MaxVertexCount = ushort.MaxValue;
	private const int MaxIndexCount = ushort.MaxValue;

	private static DeviceBuffer vertexBuffer;
	private static DeviceBuffer indexBuffer;

	static Graphics()
	{
		var vertexStructSize = (uint)Marshal.SizeOf( typeof( Vertex ) );

		vertexBuffer = Device.ResourceFactory.CreateBuffer(
			new BufferDescription( MaxVertexCount * vertexStructSize, BufferUsage.VertexBuffer | BufferUsage.Dynamic )
		);

		var uintSize = (uint)sizeof( uint );

		indexBuffer = Device.ResourceFactory.CreateBuffer(
			new BufferDescription( MaxIndexCount * uintSize, BufferUsage.IndexBuffer | BufferUsage.Dynamic )
		);
	}

	private static Matrix4x4 CreateScreenMatrix( Point2 screenSize )
	{
		var matrix = Matrix4x4.Identity;

		// Scale to fit screen
		matrix *= Matrix4x4.CreateScale( new Vector3( 1f / screenSize.X, 1f / screenSize.Y, 1 ).GetSystemVector3() );

		// Convert from [-0.5f, 0.5f] to [0.0f, 1.0f]
		matrix *= Matrix4x4.CreateScale( 2.0f );
		matrix *= Matrix4x4.CreateTranslation( new Vector3( -1f, -1f, 0 ).GetSystemVector3() );

		matrix *= Matrix4x4.CreateScale( new Vector3( 1, -1, 1 ).GetSystemVector3() );

		return matrix;
	}

	public static void Quad( Rectangle rectangle, Material material )
	{
		Quad( rectangle, new Rectangle( 0, 0, 1, 1 ), material );
	}

	public static void Quad( Rectangle rectangle, Rectangle uvs, Material material )
	{
		var screenMatrix = CreateScreenMatrix( BaseScreenSize );

		var v0 = new Vector3( rectangle.TopLeft ) * screenMatrix;
		var v1 = new Vector3( rectangle.TopRight ) * screenMatrix;
		var v2 = new Vector3( rectangle.BottomLeft ) * screenMatrix;
		var v3 = new Vector3( rectangle.BottomRight ) * screenMatrix;

		var t0 = uvs.TopLeft;
		var t1 = uvs.TopRight;
		var t2 = uvs.BottomLeft;
		var t3 = uvs.BottomRight;

		Vertex[] vertices = [
			new Vertex( v0, t0 ), new Vertex( v1, t1 ), new Vertex( v2, t2 ), new Vertex( v3, t3 )
		];

		uint[] indices = [
			3, 2, 1,
			0, 1, 2
		];

		var cmd = Render.GetCommandList();
		cmd.UpdateBuffer( vertexBuffer, 0, vertices );
		cmd.UpdateBuffer( indexBuffer, 0, indices );

		cmd.SetIndexBuffer( indexBuffer, IndexFormat.UInt32 );
		cmd.SetVertexBuffer( 0, vertexBuffer );
		cmd.SetPipeline( material.Pipeline );

		material.GetTemporaryResourceSet( out var resourceSets );

		for ( uint i = 0; i < resourceSets.Length; ++i )
			cmd.SetGraphicsResourceSet( i, resourceSets[i] );

		cmd.DrawIndexed( (uint)indices.Length );
	}
}
