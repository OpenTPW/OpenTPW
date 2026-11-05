using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SDL;

namespace OpenTPW.Graphics;

/// <summary>Records draws into one render target; nothing reaches SDL until the frame is submitted.</summary>
public sealed class GpuRenderPass
{
	private readonly GpuFrame frame;
	private readonly PassRecord record;
	private bool ended;

	internal GpuRenderPass( GpuFrame frame, PassRecord record )
	{
		this.frame = frame;
		this.record = record;
	}

	public RenderTarget Target => record.Target;

	/// <summary>Draws <paramref name="vertexCount"/> vertices. Uniform bytes go to slot 0 of each stage (b0,space1 and b0,space3).</summary>
	public void Draw( GpuPipeline pipeline, BufferSlice vertices, uint vertexCount, ReadOnlySpan<TextureBinding> textures = default,
		ReadOnlySpan<byte> vertexUniforms = default, ReadOnlySpan<byte> fragmentUniforms = default, uint firstVertex = 0, uint instances = 1 )
	{
		Check( pipeline );
		if ( pipeline.Description.VertexLayout.Stride != 0 ) frame.CheckSlice( vertices, GpuBufferUsage.Vertex, nameof(vertices) );
		Add( pipeline, vertices, null, default, vertexCount, instances, firstVertex, 0, textures, vertexUniforms, fragmentUniforms );
	}

	public void DrawIndexed( GpuPipeline pipeline, BufferSlice vertices, BufferSlice indices, SDL_GPUIndexElementSize indexSize, uint indexCount,
		ReadOnlySpan<TextureBinding> textures = default, ReadOnlySpan<byte> vertexUniforms = default, ReadOnlySpan<byte> fragmentUniforms = default,
		uint firstIndex = 0, int vertexOffset = 0, uint instances = 1 )
	{
		Check( pipeline );
		frame.CheckSlice( vertices, GpuBufferUsage.Vertex, nameof(vertices) );
		frame.CheckSlice( indices, GpuBufferUsage.Index, nameof(indices) );
		Add( pipeline, vertices, indices, indexSize, indexCount, instances, firstIndex, vertexOffset, textures, vertexUniforms, fragmentUniforms );
	}

	public void End()
	{
		if ( ended ) return;
		ended = true;
		frame.EndPass( this );
	}

	private void Check( GpuPipeline pipeline )
	{
		frame.EnsureRecording();
		if ( ended ) throw new InvalidOperationException( "This pass has ended." );
		var description = pipeline.Description;
		var target = record.Target;
		if ( description.ColorFormat != target.ColorFormat || description.DepthFormat != target.DepthFormat || description.SampleCount != target.SampleCount )
			throw new ArgumentException( "The pipeline was built for different target formats or sample count.", nameof(pipeline) );
	}

	private void Add( GpuPipeline pipeline, BufferSlice vertices, BufferSlice? indices, SDL_GPUIndexElementSize indexSize, uint count, uint instances,
		uint first, int vertexOffset, ReadOnlySpan<TextureBinding> textures, ReadOnlySpan<byte> vertexUniforms, ReadOnlySpan<byte> fragmentUniforms )
	{
		var (textureStart, textureCount) = frame.AddTextures( textures );
		var (vertexStart, vertexLength) = frame.AddUniforms( vertexUniforms );
		var (fragmentStart, fragmentLength) = frame.AddUniforms( fragmentUniforms );
		record.Draws.Add( new DrawRecord( pipeline, GpuFrame.Handle( vertices ), vertices.Offset,
			indices.HasValue, indices.HasValue ? GpuFrame.Handle( indices.Value ) : IntPtr.Zero, indices?.Offset ?? 0, indexSize,
			count, instances, first, vertexOffset, textureStart, textureCount, vertexStart, vertexLength, fragmentStart, fragmentLength ) );
	}
}

/// <summary>Helpers for passing a uniform struct as the bytes <see cref="GpuRenderPass.Draw"/> takes.</summary>
public static class GpuUniforms
{
	public static ReadOnlySpan<byte> Bytes<T>( ref readonly T value ) where T : unmanaged
		=> MemoryMarshal.AsBytes( new ReadOnlySpan<T>( in value ) );
}
