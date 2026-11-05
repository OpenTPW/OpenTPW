using SDL;

namespace OpenTPW.Graphics;

/// <summary>
/// An offscreen colour target with optional depth and multisampling. With more than one sample the pass resolves
/// into <see cref="Color"/> when it ends (replacing a separate resolve step); with one sample it draws into it directly.
/// <see cref="Color"/> can be presented, read back or sampled by a later pass.
/// </summary>
public sealed class RenderTarget : IDisposable
{
	private readonly GpuDevice device;
	public uint Width { get; private set; }
	public uint Height { get; private set; }
	public SDL_GPUTextureFormat ColorFormat { get; }
	public SDL_GPUTextureFormat? DepthFormat { get; }
	public SDL_GPUSampleCount SampleCount { get; }
	/// <summary>The single-sampled result.</summary>
	public GpuTexture Color { get; private set; } = null!;
	internal GpuTexture? Multisampled { get; private set; }
	internal GpuTexture? Depth { get; private set; }

	public RenderTarget( GpuDevice device, uint width, uint height,
		SDL_GPUTextureFormat colorFormat = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
		SDL_GPUSampleCount sampleCount = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1, bool depth = true )
	{
		this.device = device;
		ColorFormat = colorFormat;
		SampleCount = sampleCount;
		DepthFormat = depth ? device.DepthFormat : null;
		Create( width, height );
	}

	private void Create( uint width, uint height )
	{
		const SDL_GPUTextureUsageFlags colorUsage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_COLOR_TARGET;
		Width = width;
		Height = height;
		Color = GpuTexture.CreateTarget( device, width, height, ColorFormat,
			colorUsage | SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER, SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1 );
		if ( SampleCount != SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1 )
			Multisampled = GpuTexture.CreateTarget( device, width, height, ColorFormat, colorUsage, SampleCount );
		if ( DepthFormat is { } format )
			Depth = GpuTexture.CreateTarget( device, width, height, format, SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_DEPTH_STENCIL_TARGET, SampleCount );
	}

	/// <summary>Recreates the textures at a new size; pipelines stay valid because formats and sample count do not change.</summary>
	public void Resize( uint width, uint height )
	{
		if ( width == Width && height == Height ) return;
		Release();
		Create( width, height );
	}

	private void Release()
	{
		Color.Dispose();
		Multisampled?.Dispose();
		Depth?.Dispose();
		Multisampled = Depth = null;
	}

	public void Dispose() => Release();
}
