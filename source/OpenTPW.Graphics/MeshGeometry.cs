namespace OpenTPW.Graphics;

/// <summary>Static mesh buffers with optional animated vertices copied into the owning frame's upload arena.</summary>
public sealed class MeshGeometry<T> : IDisposable where T : unmanaged
{
	private readonly GpuBuffer original;
	private readonly GpuBuffer? indices;
	private readonly uint count;
	private readonly int vertexCount;
	private T[]? animated;
	public MeshGeometry( GpuDevice device, ReadOnlySpan<T> vertices, ReadOnlySpan<uint> indexData = default )
	{
		original = GpuBuffer.Create<T>( device, GpuBufferUsage.Vertex, vertices );
		vertexCount = vertices.Length;
		try { if ( !indexData.IsEmpty ) indices = GpuBuffer.Create<uint>( device, GpuBufferUsage.Index, indexData ); }
		// Index allocation can fail after the vertex upload; construction must not leak its earlier buffer.
		catch { original.Dispose(); throw; }
		count = (uint)(indexData.IsEmpty ? vertices.Length : indexData.Length);
	}

	public void UpdateVertices( ReadOnlySpan<T> vertices )
	{
		if ( vertices.Length != vertexCount ) throw new ArgumentException( "Animated vertex count changed.", nameof(vertices) );
		animated ??= new T[vertexCount];
		vertices.CopyTo( animated );
	}

	public void Draw( GpuFrame frame, GpuRenderPass pass, MeshMaterial material )
	{
		// Upload copies immediately into a frame-owned arena. Later CPU animation cannot alter an earlier draw.
		BufferSlice vertices = animated == null ? original : frame.Upload<T>( animated );
		material.Draw( pass, vertices, count, indices == null ? (BufferSlice?)null : (BufferSlice)indices );
	}

	public void Dispose() { original.Dispose(); indices?.Dispose(); }
}
