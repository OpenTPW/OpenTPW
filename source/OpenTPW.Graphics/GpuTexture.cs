using SDL;
using static SDL.SDL3;

namespace OpenTPW.Graphics;

/// <summary>A 2D texture. Pixel conversions such as the retail ARGB4444 quantisation are done by the caller.</summary>
public sealed unsafe class GpuTexture : IDisposable
{
	private readonly GpuDevice device;
	internal SDL_GPUTexture* Handle { get; private set; }
	public uint Width { get; }
	public uint Height { get; }
	public uint Levels { get; }
	public SDL_GPUTextureFormat Format { get; }
	public SDL_GPUSampleCount SampleCount { get; }

	private GpuTexture( GpuDevice device, uint width, uint height, uint levels, SDL_GPUTextureFormat format,
		SDL_GPUTextureUsageFlags usage, SDL_GPUSampleCount sampleCount )
	{
		if ( width == 0 || height == 0 ) throw new ArgumentOutOfRangeException( nameof(width), "Textures need a non-zero size." );
		this.device = device;
		(Width, Height, Levels, Format, SampleCount) = (width, height, levels, format, sampleCount);
		var info = new SDL_GPUTextureCreateInfo
		{
			type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
			format = format,
			usage = usage,
			width = width,
			height = height,
			layer_count_or_depth = 1,
			num_levels = levels,
			sample_count = sampleCount
		};
		Handle = SDL_CreateGPUTexture( device.Handle, &info );
		if ( Handle == null ) throw GraphicsException.FromSdl( $"SDL_CreateGPUTexture ({width}x{height} {format})" );
	}

	/// <summary>A sampled texture, by default 8-bit RGBA with one mip level.</summary>
	public static GpuTexture Create2D( GpuDevice device, uint width, uint height,
		SDL_GPUTextureFormat format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM, uint levels = 1 )
		=> new( device, width, height, levels, format, SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER, SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1 );

	internal static GpuTexture CreateTarget( GpuDevice device, uint width, uint height, SDL_GPUTextureFormat format,
		SDL_GPUTextureUsageFlags usage, SDL_GPUSampleCount sampleCount )
		=> new( device, width, height, 1, format, usage, sampleCount );

	/// <summary>Bytes per pixel of the 32-bit colour formats this library uploads and reads back.</summary>
	private uint PixelSize => Format is SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM or SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_B8G8R8A8_UNORM
		or SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM_SRGB or SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_B8G8R8A8_UNORM_SRGB
		? 4u : throw new NotSupportedException( $"Pixel transfers support 32-bit colour formats, not {Format}." );

	/// <summary>
	/// Replaces one mip level. The copy cycles the texture: if a submitted frame still samples it, SDL gives this upload
	/// fresh storage instead of changing pixels the GPU is reading. Frames recorded afterwards see the new pixels.
	/// </summary>
	public void Upload( ReadOnlySpan<byte> pixels, uint level = 0 )
	{
		ObjectDisposedException.ThrowIf( Handle == null, this );
		if ( device.Recording != null )
			throw new InvalidOperationException( "Upload textures between frames; a frame being recorded may already refer to the old pixels." );
		uint width = Math.Max( 1, Width >> (int)level ), height = Math.Max( 1, Height >> (int)level );
		if ( pixels.Length != checked((int)(width * height * PixelSize)) )
			throw new ArgumentException( $"Level {level} needs {width * height * PixelSize} bytes, got {pixels.Length}.", nameof(pixels) );
		var transfer = (IntPtr)Transfer.Upload( device, pixels );
		var texture = (IntPtr)Handle;
		try
		{
			device.Immediate( pass =>
			{
				var source = new SDL_GPUTextureTransferInfo { transfer_buffer = (SDL_GPUTransferBuffer*)transfer, offset = 0, pixels_per_row = width, rows_per_layer = height };
				var destination = new SDL_GPUTextureRegion { texture = (SDL_GPUTexture*)texture, mip_level = level, w = width, h = height, d = 1 };
				SDL_UploadToGPUTexture( (SDL_GPUCopyPass*)pass, &source, &destination, true );
			}, wait: false );
		}
		finally { SDL_ReleaseGPUTransferBuffer( device.Handle, (SDL_GPUTransferBuffer*)transfer ); }
	}

	/// <summary>Reads level 0 back to the CPU, waiting for all work submitted so far. For tests and screenshots.</summary>
	public byte[] Download()
	{
		ObjectDisposedException.ThrowIf( Handle == null, this );
		if ( SampleCount != SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1 ) throw new InvalidOperationException( "Read back the resolved texture." );
		uint size = Width * Height * PixelSize;
		var transfer = (IntPtr)Transfer.Create( device, SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_DOWNLOAD, size );
		var texture = (IntPtr)Handle;
		try
		{
			device.Immediate( pass =>
			{
				var source = new SDL_GPUTextureRegion { texture = (SDL_GPUTexture*)texture, w = Width, h = Height, d = 1 };
				var destination = new SDL_GPUTextureTransferInfo { transfer_buffer = (SDL_GPUTransferBuffer*)transfer, pixels_per_row = Width, rows_per_layer = Height };
				SDL_DownloadFromGPUTexture( (SDL_GPUCopyPass*)pass, &source, &destination );
			}, wait: true );
			var mapped = SDL_MapGPUTransferBuffer( device.Handle, (SDL_GPUTransferBuffer*)transfer, false );
			if ( mapped == IntPtr.Zero ) throw GraphicsException.FromSdl( nameof(SDL_MapGPUTransferBuffer) );
			try { return new ReadOnlySpan<byte>( (void*)mapped, (int)size ).ToArray(); }
			finally { SDL_UnmapGPUTransferBuffer( device.Handle, (SDL_GPUTransferBuffer*)transfer ); }
		}
		finally { SDL_ReleaseGPUTransferBuffer( device.Handle, (SDL_GPUTransferBuffer*)transfer ); }
	}

	public void Dispose()
	{
		if ( Handle == null ) return;
		var handle = (IntPtr)Handle;
		Handle = null;
		device.Release( () => SDL_ReleaseGPUTexture( device.Handle, (SDL_GPUTexture*)handle ) );
	}
}
