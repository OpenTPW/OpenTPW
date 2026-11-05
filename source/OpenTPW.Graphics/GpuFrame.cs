using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace OpenTPW.Graphics;

/// <summary>A texture and the sampler bound with it at one fragment sampler slot.</summary>
public readonly record struct TextureBinding( GpuTexture Texture, GpuSampler Sampler );

/// <summary>What a pass does with existing contents: clear colour and depth, or keep them when null.</summary>
public readonly record struct GpuClear( SDL_FColor? Color, float? Depth = 1 )
{
	public static GpuClear Load => new( null, null );
	public static GpuClear ToColor( float r, float g, float b, float a ) => new( new SDL_FColor { r = r, g = g, b = b, a = a } );
}

/// <summary>
/// One frame being recorded. Uploads and draws are only recorded here; <see cref="Submit"/> replays them in one
/// command buffer: a copy pass with all of the frame's dynamic data, then the render passes in order, then
/// presentation. Callers can therefore interleave writing per-draw data and drawing in any order.
/// </summary>
public sealed unsafe class GpuFrame
{
	private const int Alignment = 16;
	private readonly GpuDevice device;
	private readonly FrameSlot slot;
	private readonly List<PassRecord> passes = new();
	// Native handles are captured when a draw is recorded: an object disposed later in the frame is only
	// released after submission, but its wrapper no longer exposes the handle.
	private readonly List<(IntPtr Texture, IntPtr Sampler)> textures = new();
	private readonly List<Action> releases = new();
	private GpuRenderPass? open;
	private (IntPtr Texture, uint Width, uint Height, SDL_GPUFilter Filter)? present;
	private bool submitted;
	public long Number { get; }

	internal GpuFrame( GpuDevice device, FrameSlot slot, long number )
	{
		this.device = device;
		this.slot = slot;
		Number = number;
		slot.Staging.Clear();
		slot.Uniforms.Clear();
	}

	/// <summary>
	/// Copies vertex or index data into this frame's dynamic buffer and returns its range. Every call gets its own
	/// range, and the buffer is not reused until this frame has finished on the GPU.
	/// </summary>
	public BufferSlice Upload<T>( ReadOnlySpan<T> data ) where T : unmanaged
	{
		EnsureRecording();
		var bytes = MemoryMarshal.AsBytes( data );
		if ( bytes.IsEmpty ) throw new ArgumentException( "No data to upload.", nameof(data) );
		var staging = slot.Staging;
		while ( staging.Count % Alignment != 0 ) staging.Add( 0 );
		uint offset = (uint)staging.Count;
		staging.AddRange( bytes );
		return new BufferSlice( null, offset, (uint)bytes.Length, Number );
	}

	public BufferSlice Upload<T>( T[] data ) where T : unmanaged => Upload( (ReadOnlySpan<T>)data );

	public GpuRenderPass BeginPass( RenderTarget target, GpuClear clear )
	{
		EnsureRecording();
		if ( open != null ) throw new InvalidOperationException( "End the previous pass first." );
		var record = new PassRecord( target, clear );
		passes.Add( record );
		return open = new GpuRenderPass( this, record );
	}

	internal void EndPass( GpuRenderPass pass )
	{
		if ( open != pass ) throw new InvalidOperationException( "This pass is not open." );
		open = null;
	}

	/// <summary>Scales <paramref name="source"/> onto the attached window's swapchain after all passes.</summary>
	public void Present( GpuTexture source, SDL_GPUFilter filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR )
	{
		EnsureRecording();
		if ( device.Window == null ) throw new InvalidOperationException( "No window is attached." );
		ObjectDisposedException.ThrowIf( source.Handle == null, source );
		present = ((IntPtr)source.Handle, source.Width, source.Height, filter);
	}

	/// <summary>SDL's per-stage sampler slot limit; lobby-model and test use all 16.</summary>
	internal const int MaxSamplers = 16;

	internal (int Start, int Count) AddTextures( ReadOnlySpan<TextureBinding> bindings )
	{
		if ( bindings.Length > MaxSamplers ) throw new ArgumentException( $"At most {MaxSamplers} textures per draw.", nameof(bindings) );
		int start = textures.Count;
		foreach ( var binding in bindings )
		{
			ObjectDisposedException.ThrowIf( binding.Texture.Handle == null, binding.Texture );
			textures.Add( ((IntPtr)binding.Texture.Handle, (IntPtr)binding.Sampler.Handle) );
		}
		return (start, bindings.Length);
	}

	internal (int Start, int Length) AddUniforms( ReadOnlySpan<byte> bytes )
	{
		if ( bytes.IsEmpty ) return (0, 0);
		var uniforms = slot.Uniforms;
		while ( uniforms.Count % Alignment != 0 ) uniforms.Add( 0 );
		int start = uniforms.Count;
		uniforms.AddRange( bytes );
		return (start, bytes.Length);
	}

	internal void CheckSlice( BufferSlice slice, GpuBufferUsage usage, string name )
	{
		if ( slice.Buffer == null )
		{
			if ( slice.Frame != Number ) throw new ArgumentException( $"{name} was uploaded in frame {slice.Frame}, not this frame ({Number}).", name );
		}
		else if ( (slice.Buffer.Usage & usage) == 0 ) throw new ArgumentException( $"{name} is not a {usage} buffer.", name );
		else if ( slice.Buffer.Handle == null ) throw new ObjectDisposedException( name );
	}

	internal static unsafe IntPtr Handle( BufferSlice slice ) => slice.Buffer != null ? (IntPtr)slice.Buffer.Handle : IntPtr.Zero;

	internal void ReleaseAfterSubmit( Action release ) => releases.Add( release );

	internal void EnsureRecording()
	{
		if ( submitted ) throw new InvalidOperationException( "This frame has already been submitted." );
	}

	/// <summary>Replays the recorded frame into one command buffer and submits it. The frame slot keeps its fence.</summary>
	public void Submit()
	{
		EnsureRecording();
		if ( open != null ) throw new InvalidOperationException( "End the open pass before submitting." );
		submitted = true;
		try
		{
			var commands = SDL_AcquireGPUCommandBuffer( device.Handle );
			if ( commands == null ) throw GraphicsException.FromSdl( nameof(SDL_AcquireGPUCommandBuffer) );
			UploadDynamic( commands );
			foreach ( var pass in passes ) Replay( commands, pass );
			PresentTo( commands );
			var fence = SDL_SubmitGPUCommandBufferAndAcquireFence( commands );
			if ( fence == null ) throw GraphicsException.FromSdl( nameof(SDL_SubmitGPUCommandBufferAndAcquireFence) );
			slot.Fence = fence;
		}
		finally
		{
			device.EndRecording( this );
			foreach ( var release in releases ) release();
			releases.Clear();
		}
	}

	private void UploadDynamic( SDL_GPUCommandBuffer* commands )
	{
		var staging = CollectionsMarshal.AsSpan( slot.Staging );
		if ( staging.IsEmpty ) return;
		// The slot's previous frame has completed (BeginFrame waited for it), so its buffers can be rewritten.
		slot.Reserve( device, (uint)staging.Length );
		var mapped = SDL_MapGPUTransferBuffer( device.Handle, slot.Transfer, false );
		if ( mapped == IntPtr.Zero ) throw GraphicsException.FromSdl( nameof(SDL_MapGPUTransferBuffer) );
		staging.CopyTo( new Span<byte>( (void*)mapped, staging.Length ) );
		SDL_UnmapGPUTransferBuffer( device.Handle, slot.Transfer );
		var copy = SDL_BeginGPUCopyPass( commands );
		var source = new SDL_GPUTransferBufferLocation { transfer_buffer = slot.Transfer, offset = 0 };
		var destination = new SDL_GPUBufferRegion { buffer = slot.Dynamic, offset = 0, size = (uint)staging.Length };
		SDL_UploadToGPUBuffer( copy, &source, &destination, false );
		SDL_EndGPUCopyPass( copy );
	}

	private void Replay( SDL_GPUCommandBuffer* commands, PassRecord pass )
	{
		bool multisampled = pass.Multisampled != IntPtr.Zero;
		var color = new SDL_GPUColorTargetInfo
		{
			texture = (SDL_GPUTexture*)(multisampled ? pass.Multisampled : pass.Color),
			load_op = pass.Clear.Color.HasValue ? SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR : SDL_GPULoadOp.SDL_GPU_LOADOP_LOAD,
			clear_color = pass.Clear.Color ?? default,
			// Multisampled contents are resolved into Color and not kept, as with the Veldrid resolve.
			store_op = multisampled ? SDL_GPUStoreOp.SDL_GPU_STOREOP_RESOLVE : SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE,
			resolve_texture = multisampled ? (SDL_GPUTexture*)pass.Color : null
		};
		var depth = new SDL_GPUDepthStencilTargetInfo
		{
			texture = (SDL_GPUTexture*)pass.Depth,
			load_op = pass.Clear.Depth.HasValue ? SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR : SDL_GPULoadOp.SDL_GPU_LOADOP_LOAD,
			clear_depth = pass.Clear.Depth ?? 1,
			store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE,
			stencil_load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
			stencil_store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE
		};
		var render = SDL_BeginGPURenderPass( commands, &color, 1, pass.Depth != IntPtr.Zero ? &depth : null );
		if ( render == null ) throw GraphicsException.FromSdl( nameof(SDL_BeginGPURenderPass) );
		GpuPipeline? bound = null;
		var uniforms = CollectionsMarshal.AsSpan( slot.Uniforms );
		var bindings = stackalloc SDL_GPUTextureSamplerBinding[MaxSamplers];
		foreach ( var draw in pass.Draws )
		{
			if ( draw.Pipeline != bound ) SDL_BindGPUGraphicsPipeline( render, (bound = draw.Pipeline).Handle );
			if ( draw.Pipeline.Description.VertexLayout.Stride != 0 )
			{
				var vertices = Binding( draw.VertexBuffer, draw.VertexOffsetBytes );
				SDL_BindGPUVertexBuffers( render, 0, &vertices, 1 );
			}
			if ( draw.TextureCount > 0 )
			{
				for ( int i = 0; i < draw.TextureCount; i++ )
				{
					var binding = textures[draw.TextureStart + i];
					bindings[i] = new SDL_GPUTextureSamplerBinding { texture = (SDL_GPUTexture*)binding.Texture, sampler = (SDL_GPUSampler*)binding.Sampler };
				}
				SDL_BindGPUFragmentSamplers( render, 0, bindings, (uint)draw.TextureCount );
			}
			// SDL copies pushed uniform data per call, so every draw keeps its own values.
			fixed ( byte* data = uniforms )
			{
				if ( draw.VertexUniformLength > 0 )
					SDL_PushGPUVertexUniformData( commands, 0, (IntPtr)(data + draw.VertexUniformStart), (uint)draw.VertexUniformLength );
				if ( draw.FragmentUniformLength > 0 )
					SDL_PushGPUFragmentUniformData( commands, 0, (IntPtr)(data + draw.FragmentUniformStart), (uint)draw.FragmentUniformLength );
			}
			if ( draw.Indexed )
			{
				var binding = Binding( draw.IndexBuffer, draw.IndexOffsetBytes );
				SDL_BindGPUIndexBuffer( render, &binding, draw.IndexSize );
				SDL_DrawGPUIndexedPrimitives( render, draw.Count, draw.Instances, draw.First, draw.VertexOffset, 0 );
			}
			else SDL_DrawGPUPrimitives( render, draw.Count, draw.Instances, draw.First, 0 );
		}
		SDL_EndGPURenderPass( render );
	}

	/// <summary>A zero handle selects the frame's dynamic buffer.</summary>
	private SDL_GPUBufferBinding Binding( IntPtr buffer, uint offset ) => new()
	{
		buffer = buffer != IntPtr.Zero ? (SDL_GPUBuffer*)buffer : slot.Dynamic,
		offset = offset
	};

	private void PresentTo( SDL_GPUCommandBuffer* commands )
	{
		if ( present is not { } request || device.Window == null ) return;
		SDL_GPUTexture* swapchain;
		uint width, height;
		GraphicsException.Check( SDL_WaitAndAcquireGPUSwapchainTexture( commands, device.Window, &swapchain, &width, &height ),
			nameof(SDL_WaitAndAcquireGPUSwapchainTexture) );
		// A minimised window has no swapchain image; the frame's offscreen work still runs.
		if ( swapchain == null ) return;
		var blit = new SDL_GPUBlitInfo
		{
			source = new SDL_GPUBlitRegion { texture = (SDL_GPUTexture*)request.Texture, w = request.Width, h = request.Height },
			destination = new SDL_GPUBlitRegion { texture = swapchain, w = width, h = height },
			load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_DONT_CARE,
			filter = request.Filter
		};
		SDL_BlitGPUTexture( commands, &blit );
	}
}

internal sealed class PassRecord
{
	public readonly IntPtr Color, Multisampled, Depth;
	public readonly GpuClear Clear;
	public readonly RenderTarget Target;
	public readonly List<DrawRecord> Draws = new();

	public unsafe PassRecord( RenderTarget target, GpuClear clear )
	{
		Target = target;
		Clear = clear;
		// Handles are taken now so a resize later in the frame cannot redirect an earlier pass.
		Color = (IntPtr)target.Color.Handle;
		Multisampled = target.Multisampled != null ? (IntPtr)target.Multisampled.Handle : IntPtr.Zero;
		Depth = target.Depth != null ? (IntPtr)target.Depth.Handle : IntPtr.Zero;
	}
}

internal readonly record struct DrawRecord( GpuPipeline Pipeline, IntPtr VertexBuffer, uint VertexOffsetBytes,
	bool Indexed, IntPtr IndexBuffer, uint IndexOffsetBytes, SDL_GPUIndexElementSize IndexSize,
	uint Count, uint Instances, uint First, int VertexOffset, int TextureStart, int TextureCount,
	int VertexUniformStart, int VertexUniformLength, int FragmentUniformStart, int FragmentUniformLength );
