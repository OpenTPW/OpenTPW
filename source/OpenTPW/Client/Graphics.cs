using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace OpenTPW;

internal static partial class Graphics
{
	private static readonly uint[] QuadIndices = { 3, 2, 1, 0, 1, 2 };
	private static readonly DeviceBuffer indexBuffer;

	static Graphics()
	{
		indexBuffer = Device.ResourceFactory.CreateBuffer( new BufferDescription( (uint)(QuadIndices.Length * sizeof( uint )), BufferUsage.IndexBuffer ) );
		Device.UpdateBuffer( indexBuffer, 0, QuadIndices );
	}

	private static Matrix4x4 CreateScreenMatrix( Point2 screenSize )
	{
		var matrix = Matrix4x4.Identity;

		// Scale to fit screen
		matrix *= Matrix4x4.CreateScale( new Vector3( 1f / screenSize.X, 1f / screenSize.Y, 1 ).GetSystemVector3() );

		// Convert from [-0.5f, 0.5f] to [0.0f, 1.0f]
		matrix *= Matrix4x4.CreateScale( 2.0f );
		matrix *= Matrix4x4.CreateTranslation( new Vector3( -1f, -1f, 0 ).GetSystemVector3() );

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

		Vertex[] vertices = [new( v0, t0 ), new( v1, t1 ), new( v2, t2 ), new( v3, t3 )];

		// A buffer per quad and frame: updating one shared buffer inside the render pass would end and
		// restart the pass for every quad.
		var vertexBuffer = Render.RentFrameBuffer( BufferUsage.VertexBuffer, (uint)(vertices.Length * Marshal.SizeOf<Vertex>()) );
		Renderer.Write<Vertex>( vertexBuffer, vertices );
		var cmd = Render.CommandList;

		cmd.SetIndexBuffer( indexBuffer, IndexFormat.UInt32 );
		cmd.SetVertexBuffer( 0, vertexBuffer );
		cmd.SetPipeline( material.Pipeline );

		var resourceSets = material.GetResourceSets();

		for ( uint i = 0; i < resourceSets.Length; ++i )
			cmd.SetGraphicsResourceSet( i, resourceSets[i] );

		cmd.DrawIndexed( (uint)QuadIndices.Length );
	}
}
