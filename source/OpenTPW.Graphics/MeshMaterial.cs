using SDL;

namespace OpenTPW.Graphics;

/// <summary>Game-independent mesh render state, shared by world models and screen-space geometry.</summary>
public sealed record MeshRenderState
{
	public GpuBlend Blend { get; init; } = GpuBlend.Alpha;
	public bool DepthTest { get; init; } = true;
	public bool DepthWrite { get; init; } = true;
	public GpuCull Cull { get; init; } = GpuCull.Back;
	public SDL_GPUFrontFace FrontFace { get; init; } = SDL_GPUFrontFace.SDL_GPU_FRONTFACE_CLOCKWISE;
}

/// <summary>Reusable mesh bindings and pipeline state; the games supply shaders, textures and uniform bytes.</summary>
public sealed class MeshMaterial( GpuDevice device, ShaderProgram program, GpuVertexLayout layout, MeshRenderState state )
{
	private TextureBinding[] textures = [];
	private byte[] vertexUniforms = [], fragmentUniforms = [];
	private RenderTarget? preparedTarget;
	private GpuPipeline? preparedPipeline;
	public void SetTextures( ReadOnlySpan<TextureBinding> bindings )
	{
		if ( textures.Length != bindings.Length ) textures = new TextureBinding[bindings.Length];
		bindings.CopyTo( textures );
	}

	public void SetTexture( GpuTexture texture, GpuSampler sampler )
	{
		if ( textures.Length != 1 ) textures = new TextureBinding[1];
		textures[0] = new( texture, sampler );
	}

	public void SetUniforms( ReadOnlySpan<byte> vertex, ReadOnlySpan<byte> fragment )
	{
		if ( vertexUniforms.Length != vertex.Length ) vertexUniforms = new byte[vertex.Length];
		if ( fragmentUniforms.Length != fragment.Length ) fragmentUniforms = new byte[fragment.Length];
		vertex.CopyTo( vertexUniforms );
		fragment.CopyTo( fragmentUniforms );
	}

	/// <summary>May be called when loading a scene to create pipelines before its first rendered frame.</summary>
	public GpuPipeline Prepare( RenderTarget target )
	{
		if ( ReferenceEquals( target, preparedTarget ) && preparedPipeline != null ) return preparedPipeline;
		preparedTarget = target;
		return preparedPipeline = device.GetPipeline( GpuPipelineDescription.For( target, program, layout ) with
		{
			Blend = state.Blend, DepthTest = state.DepthTest, DepthWrite = state.DepthWrite,
			DepthCompare = !state.DepthTest && !state.DepthWrite ? SDL_GPUCompareOp.SDL_GPU_COMPAREOP_ALWAYS : SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS,
			Cull = state.Cull, FrontFace = state.FrontFace
		} );
	}

	public void Draw( GpuRenderPass pass, BufferSlice vertices, uint count, BufferSlice? indices = null )
	{
		var pipeline = Prepare( pass.Target );
		if ( indices is { } index ) pass.DrawIndexed( pipeline, vertices, index, SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_32BIT,
			count, textures, vertexUniforms, fragmentUniforms );
		else pass.Draw( pipeline, vertices, count, textures, vertexUniforms, fragmentUniforms );
	}
}
