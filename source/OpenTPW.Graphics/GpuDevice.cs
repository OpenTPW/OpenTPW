using SDL;
using static SDL.SDL3;

namespace OpenTPW.Graphics;

/// <summary>Creation options for <see cref="GpuDevice"/>.</summary>
public sealed record GpuDeviceOptions
{
	/// <summary>SDL driver names to try in order; null selects Direct3D 12 then Vulkan on Windows, Vulkan elsewhere.</summary>
	public IReadOnlyList<string>? Drivers { get; init; }
	/// <summary>Frames the GPU may still be executing while the CPU records the next one (1-3).</summary>
	public int FramesInFlight { get; init; } = 2;
	/// <summary>Enables the backend's validation layer.</summary>
	public bool Debug { get; init; }
}

/// <summary>
/// An SDL GPU device and the per-frame state shared by both games. The device does not need a window:
/// offscreen rendering, tests and screenshots use the same path, and <c>AttachWindow</c> adds presentation.
/// All calls are made on the rendering thread.
/// </summary>
public sealed unsafe class GpuDevice : IDisposable
{
	internal SDL_GPUDevice* Handle { get; private set; }
	internal SDL_Window* Window { get; private set; }
	public string Driver { get; }
	/// <summary>The compiled shader format this backend consumes (DXIL on Direct3D 12, SPIR-V on Vulkan).</summary>
	public SDL_GPUShaderFormat ShaderFormat { get; }
	/// <summary>The first depth format the device supports as a depth target, preferring the current D32/S8.</summary>
	public SDL_GPUTextureFormat DepthFormat { get; }
	public int FramesInFlight { get; }

	private readonly FrameSlot[] slots;
	private readonly Dictionary<GpuPipelineDescription, GpuPipeline> pipelines = new();
	private readonly Dictionary<SamplerPreset, GpuSampler> samplers = new();
	private long frameNumber;
	internal GpuFrame? Recording { get; private set; }

	private GpuDevice( SDL_GPUDevice* handle, GpuDeviceOptions options )
	{
		Handle = handle;
		FramesInFlight = options.FramesInFlight;
		Driver = SDL_GetGPUDeviceDriver( handle ) ?? "unknown";
		var formats = SDL_GetGPUShaderFormats( handle );
		ShaderFormat = (formats & SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL) != 0 && Driver == "direct3d12"
			? SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL
			: (formats & SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV) != 0 ? SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV
			: throw new GraphicsException( $"The {Driver} backend accepts neither DXIL nor SPIR-V." );
		DepthFormat = new[]
		{
			SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D32_FLOAT_S8_UINT,
			SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D24_UNORM_S8_UINT,
			SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D32_FLOAT,
			SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D16_UNORM,
		}.First( format => SDL_GPUTextureSupportsFormat( handle, format, SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
			SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_DEPTH_STENCIL_TARGET ) );
		slots = Enumerable.Range( 0, FramesInFlight ).Select( _ => new FrameSlot() ).ToArray();
	}

	public static GpuDevice Create( GpuDeviceOptions? options = null )
	{
		options ??= new();
		if ( options.FramesInFlight is < 1 or > 3 ) throw new ArgumentOutOfRangeException( nameof(options), "FramesInFlight must be 1-3." );
		var drivers = options.Drivers ?? (OperatingSystem.IsWindows() ? ["direct3d12", "vulkan"] : ["vulkan"]);
		var formats = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL | SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV;
		// SDL's GPU backends need the video subsystem even without a window; the device keeps one reference to it.
		GraphicsException.Check( SDL_InitSubSystem( SDL_InitFlags.SDL_INIT_VIDEO ), nameof(SDL_InitSubSystem) );
		var errors = new List<string>();
		foreach ( var driver in drivers )
		{
			var handle = SDL_CreateGPUDevice( formats, options.Debug, driver );
			if ( handle != null )
			{
				try { return new GpuDevice( handle, options ); }
				catch { SDL_DestroyGPUDevice( handle ); SDL_QuitSubSystem( SDL_InitFlags.SDL_INIT_VIDEO ); throw; }
			}
			errors.Add( $"{driver}: {SDL_GetError()}" );
		}
		SDL_QuitSubSystem( SDL_InitFlags.SDL_INIT_VIDEO );
		throw new GraphicsException( "No SDL GPU backend could be created (" + string.Join( "; ", errors ) + ")." );
	}

	/// <summary>Claims a window for presentation. The caller keeps ownership of the window and its events.</summary>
	public void AttachWindow( SDL_Window* window, SDL_GPUPresentMode presentMode = SDL_GPUPresentMode.SDL_GPU_PRESENTMODE_VSYNC )
	{
		ObjectDisposedException.ThrowIf( Handle == null, this );
		if ( Window != null ) throw new InvalidOperationException( "A window is already attached." );
		GraphicsException.Check( SDL_ClaimWindowForGPUDevice( Handle, window ), nameof(SDL_ClaimWindowForGPUDevice) );
		Window = window;
		GraphicsException.Check( SDL_SetGPUSwapchainParameters( Handle, window,
			SDL_GPUSwapchainComposition.SDL_GPU_SWAPCHAINCOMPOSITION_SDR, presentMode ), nameof(SDL_SetGPUSwapchainParameters) );
		GraphicsException.Check( SDL_SetGPUAllowedFramesInFlight( Handle, (uint)FramesInFlight ), nameof(SDL_SetGPUAllowedFramesInFlight) );
	}

	public void AttachWindow( IntPtr window, SDL_GPUPresentMode presentMode = SDL_GPUPresentMode.SDL_GPU_PRESENTMODE_VSYNC )
		=> AttachWindow( (SDL_Window*)window, presentMode );

	public void DetachWindow()
	{
		if ( Window == null ) return;
		WaitForIdle();
		SDL_ReleaseWindowFromGPUDevice( Handle, Window );
		Window = null;
	}

	/// <summary>The swapchain format of the attached window, for pipelines that draw straight to it.</summary>
	public SDL_GPUTextureFormat SwapchainFormat => Window != null ? SDL_GetGPUSwapchainTextureFormat( Handle, Window )
		: throw new InvalidOperationException( "No window is attached." );

	/// <summary>The highest of 1, 2, 4 and 8 samples that <paramref name="format"/> supports as a render target.</summary>
	public SDL_GPUSampleCount MaxSampleCount( SDL_GPUTextureFormat format ) => new[]
	{
		SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_8, SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_4, SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_2,
	}.FirstOrDefault( count => SDL_GPUTextureSupportsSampleCount( Handle, format, count ), SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1 );

	/// <summary>
	/// Starts recording a frame. Waits until the last frame that used this frame slot has finished on the GPU,
	/// so the slot's dynamic buffers can be rewritten.
	/// </summary>
	public GpuFrame BeginFrame()
	{
		ObjectDisposedException.ThrowIf( Handle == null, this );
		if ( Recording != null ) throw new InvalidOperationException( "The previous frame has not been submitted." );
		var number = ++frameNumber;
		var slot = slots[number % FramesInFlight];
		slot.WaitAndRelease( this );
		return Recording = new GpuFrame( this, slot, number );
	}

	internal void EndRecording( GpuFrame frame )
	{
		if ( Recording != frame ) throw new InvalidOperationException( "This frame is not being recorded." );
		Recording = null;
	}

	/// <summary>Blocks until every submitted command buffer has completed.</summary>
	public void WaitForIdle()
	{
		if ( Handle == null ) return;
		GraphicsException.Check( SDL_WaitForGPUIdle( Handle ), nameof(SDL_WaitForGPUIdle) );
		foreach ( var slot in slots ) slot.WaitAndRelease( this );
	}

	public GpuPipeline GetPipeline( GpuPipelineDescription description )
	{
		ObjectDisposedException.ThrowIf( Handle == null, this );
		if ( !pipelines.TryGetValue( description, out var pipeline ) )
			pipelines.Add( description, pipeline = new GpuPipeline( this, description ) );
		return pipeline;
	}

	public GpuSampler GetSampler( SamplerPreset preset )
	{
		ObjectDisposedException.ThrowIf( Handle == null, this );
		if ( !samplers.TryGetValue( preset, out var sampler ) ) samplers.Add( preset, sampler = new GpuSampler( this, preset ) );
		return sampler;
	}

	/// <summary>
	/// Releases a GPU object. SDL already defers destruction while submitted work uses it, but a frame being
	/// recorded has not reached SDL yet, so releases requested during recording wait until that frame is submitted.
	/// </summary>
	internal void Release( Action release )
	{
		if ( Handle == null ) return;
		if ( Recording != null ) Recording.ReleaseAfterSubmit( release );
		else release();
	}

	/// <summary>Records and submits a one-off copy, for static uploads and readbacks.</summary>
	internal void Immediate( Action<IntPtr> copy, bool wait )
	{
		var commands = SDL_AcquireGPUCommandBuffer( Handle );
		if ( commands == null ) throw GraphicsException.FromSdl( nameof(SDL_AcquireGPUCommandBuffer) );
		var pass = SDL_BeginGPUCopyPass( commands );
		copy( (IntPtr)pass );
		SDL_EndGPUCopyPass( pass );
		if ( !wait )
		{
			GraphicsException.Check( SDL_SubmitGPUCommandBuffer( commands ), nameof(SDL_SubmitGPUCommandBuffer) );
			return;
		}
		var fence = SDL_SubmitGPUCommandBufferAndAcquireFence( commands );
		if ( fence == null ) throw GraphicsException.FromSdl( nameof(SDL_SubmitGPUCommandBufferAndAcquireFence) );
		try { GraphicsException.Check( SDL_WaitForGPUFences( Handle, true, &fence, 1 ), nameof(SDL_WaitForGPUFences) ); }
		finally { SDL_ReleaseGPUFence( Handle, fence ); }
	}

	public void Dispose()
	{
		if ( Handle == null ) return;
		if ( Recording != null ) throw new InvalidOperationException( "Submit the frame being recorded before disposing the device." );
		WaitForIdle();
		foreach ( var pipeline in pipelines.Values ) pipeline.Destroy();
		foreach ( var sampler in samplers.Values ) sampler.Destroy();
		pipelines.Clear();
		samplers.Clear();
		foreach ( var slot in slots ) slot.Destroy( this );
		if ( Window != null ) SDL_ReleaseWindowFromGPUDevice( Handle, Window );
		Window = null;
		SDL_DestroyGPUDevice( Handle );
		Handle = null;
		SDL_QuitSubSystem( SDL_InitFlags.SDL_INIT_VIDEO );
	}
}

/// <summary>Resources owned by one of the frames that may be in flight at the same time.</summary>
internal sealed unsafe class FrameSlot
{
	public SDL_GPUFence* Fence;
	public SDL_GPUBuffer* Dynamic;
	public uint DynamicCapacity;
	public SDL_GPUTransferBuffer* Transfer;
	public uint TransferCapacity;
	public readonly List<byte> Staging = new();
	public readonly List<byte> Uniforms = new();

	/// <summary>Waits for this slot's previous submission; afterwards nothing on the GPU reads its buffers.</summary>
	public void WaitAndRelease( GpuDevice device )
	{
		if ( Fence == null ) return;
		var fence = Fence;
		GraphicsException.Check( SDL_WaitForGPUFences( device.Handle, true, &fence, 1 ), nameof(SDL_WaitForGPUFences) );
		SDL_ReleaseGPUFence( device.Handle, fence );
		Fence = null;
	}

	/// <summary>Grows the dynamic vertex/index buffer and its upload buffer to hold <paramref name="size"/> bytes.</summary>
	public void Reserve( GpuDevice device, uint size )
	{
		if ( size <= DynamicCapacity ) return;
		uint capacity = Math.Max( 64 * 1024u, System.Numerics.BitOperations.RoundUpToPowerOf2( size ) );
		if ( Dynamic != null ) SDL_ReleaseGPUBuffer( device.Handle, Dynamic );
		if ( Transfer != null ) SDL_ReleaseGPUTransferBuffer( device.Handle, Transfer );
		var buffer = new SDL_GPUBufferCreateInfo
		{
			usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX | SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX,
			size = capacity
		};
		Dynamic = SDL_CreateGPUBuffer( device.Handle, &buffer );
		var transfer = new SDL_GPUTransferBufferCreateInfo { usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD, size = capacity };
		Transfer = SDL_CreateGPUTransferBuffer( device.Handle, &transfer );
		if ( Dynamic == null || Transfer == null ) throw GraphicsException.FromSdl( "Dynamic buffer creation" );
		DynamicCapacity = TransferCapacity = capacity;
	}

	public void Destroy( GpuDevice device )
	{
		WaitAndRelease( device );
		if ( Dynamic != null ) SDL_ReleaseGPUBuffer( device.Handle, Dynamic );
		if ( Transfer != null ) SDL_ReleaseGPUTransferBuffer( device.Handle, Transfer );
		Dynamic = null;
		Transfer = null;
		DynamicCapacity = TransferCapacity = 0;
	}
}
