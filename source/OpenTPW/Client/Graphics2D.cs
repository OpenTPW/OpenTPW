using System.Numerics;
using OpenTPW.Graphics;

namespace OpenTPW;

/// <summary>Screen-space HUD quads recorded through the shared mesh material and frame upload arena.</summary>
internal static partial class Graphics2D
{
	private static readonly uint[] QuadIndices = [3, 2, 1, 0, 1, 2];
	private static readonly GpuBuffer indexBuffer = GpuBuffer.Create<uint>( Device, GpuBufferUsage.Index, QuadIndices );
	private static Matrix4x4 CreateScreenMatrix( Point2 size ) => Matrix4x4.CreateScale( 2f / size.X, 2f / size.Y, 2 ) * Matrix4x4.CreateTranslation( -1, -1, 0 );
	public static void Quad( Rectangle rectangle, Material material ) => Quad( rectangle, new Rectangle( 0, 0, 1, 1 ), material );
	public static void Quad( Rectangle rectangle, Rectangle uvs, Material material )
	{
		var matrix = CreateScreenMatrix( BaseScreenSize );
		Vertex[] vertices = [new( new Vector3( rectangle.TopLeft ) * matrix, uvs.TopLeft ),
			new( new Vector3( rectangle.TopRight ) * matrix, uvs.TopRight ),
			new( new Vector3( rectangle.BottomLeft ) * matrix, uvs.BottomLeft ),
			new( new Vector3( rectangle.BottomRight ) * matrix, uvs.BottomRight )];
		var buffer = Render.Frame.Upload<Vertex>( vertices );
		material.Native.Draw( Render.Pass, buffer, 6, indexBuffer );
	}
}
