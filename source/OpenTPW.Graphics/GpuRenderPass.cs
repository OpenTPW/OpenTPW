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
	private GpuViewport viewport;

	internal GpuRenderPass( GpuFrame frame, PassRecord record )
	{
		this.frame = frame;
		this.record = record;
		viewport = new( 0, 0, record.Target.Width, record.Target.Height );
	}

	public RenderTarget Target => record.Target;

	/// <summary>Viewport captured by subsequent draws; changing it cannot redirect previously recorded geometry.</summary>
	public GpuViewport Viewport
	{
		get => viewport;
		set
		{
			frame.EnsureRecording();
			if ( ended ) throw new InvalidOperationException( "This pass has ended." );
			if ( !float.IsFinite( value.X ) || !float.IsFinite( value.Y ) || !float.IsFinite( value.Width ) || !float.IsFinite( value.Height ) || value.Width <= 0 || value.Height <= 0 )
				throw new ArgumentOutOfRangeException( nameof(value) );
			viewport = value;
		}
	}

	/// <summary>Draws <paramref name="vertexCount"/> vertices; <paramref name="scissor"/> limits the draw to a pixel rectangle, null draws into the whole target. Uniform bytes go to slot 0 of each stage (b0,space1 and b0,space3).</summary>
	public void Draw( GpuPipeline pipeline, BufferSlice vertices, uint vertexCount, ReadOnlySpan<TextureBinding> textures = default,
		ReadOnlySpan<byte> vertexUniforms = default, ReadOnlySpan<byte> fragmentUniforms = default, uint firstVertex = 0, uint instances = 1, GpuScissor? scissor = null )
	{
		Check( pipeline );
		if ( pipeline.Description.VertexLayout.Stride != 0 ) frame.CheckSlice( vertices, GpuBufferUsage.Vertex, nameof(vertices) );
		Add( pipeline, vertices, null, default, vertexCount, instances, firstVertex, 0, textures, vertexUniforms, fragmentUniforms, scissor );
	}

	public void DrawIndexed( GpuPipeline pipeline, BufferSlice vertices, BufferSlice indices, SDL_GPUIndexElementSize indexSize, uint indexCount,
		ReadOnlySpan<TextureBinding> textures = default, ReadOnlySpan<byte> vertexUniforms = default, ReadOnlySpan<byte> fragmentUniforms = default,
		uint firstIndex = 0, int vertexOffset = 0, uint instances = 1, GpuScissor? scissor = null )
	{
		Check( pipeline );
		frame.CheckSlice( vertices, GpuBufferUsage.Vertex, nameof(vertices) );
		frame.CheckSlice( indices, GpuBufferUsage.Index, nameof(indices) );
		Add( pipeline, vertices, indices, indexSize, indexCount, instances, firstIndex, vertexOffset, textures, vertexUniforms, fragmentUniforms, scissor );
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
		uint first, int vertexOffset, ReadOnlySpan<TextureBinding> textures, ReadOnlySpan<byte> vertexUniforms, ReadOnlySpan<byte> fragmentUniforms, GpuScissor? scissor )
	{
		var (textureStart, textureCount) = frame.AddTextures( textures );
		var (vertexStart, vertexLength) = frame.AddUniforms( vertexUniforms );
		var (fragmentStart, fragmentLength) = frame.AddUniforms( fragmentUniforms );
		record.Draws.Add( new DrawRecord( pipeline, GpuFrame.Handle( vertices ), vertices.Offset,
			indices.HasValue, indices.HasValue ? GpuFrame.Handle( indices.Value ) : IntPtr.Zero, indices?.Offset ?? 0, indexSize,
			count, instances, first, vertexOffset, textureStart, textureCount, vertexStart, vertexLength, fragmentStart, fragmentLength, viewport, scissor ) );
	}
}

/// <summary>Helpers for passing a uniform struct as the bytes <see cref="GpuRenderPass.Draw"/> takes.</summary>
public static class GpuUniforms
{
	public static ReadOnlySpan<byte> Bytes<T>( ref readonly T value ) where T : unmanaged
		=> MemoryMarshal.AsBytes( new ReadOnlySpan<T>( in value ) );
}
