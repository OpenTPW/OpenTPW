using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace OpenTPW.Graphics;

[Flags]
public enum GpuBufferUsage
{
	Vertex = 1,
	Index = 2,
}

/// <summary>
/// Vertex or index data for a draw: a range of a static <see cref="GpuBuffer"/>, or of the current frame's
/// dynamic buffer when <see cref="Buffer"/> is null (returned by <c>GpuFrame.Upload</c>).
/// </summary>
public readonly record struct BufferSlice( GpuBuffer? Buffer, uint Offset, uint Size, long Frame = 0 )
{
	public static implicit operator BufferSlice( GpuBuffer buffer ) => new( buffer, 0, buffer.Size );
}

/// <summary>Static GPU data uploaded once, such as model and terrain geometry.</summary>
public sealed unsafe class GpuBuffer : IDisposable
{
	private readonly GpuDevice device;
	internal SDL_GPUBuffer* Handle { get; private set; }
	public uint Size { get; }
	public GpuBufferUsage Usage { get; }

	private GpuBuffer( GpuDevice device, GpuBufferUsage usage, uint size )
	{
		this.device = device;
		Usage = usage;
		Size = size;
		var info = new SDL_GPUBufferCreateInfo { usage = ToSdl( usage ), size = size };
		Handle = SDL_CreateGPUBuffer( device.Handle, &info );
		if ( Handle == null ) throw GraphicsException.FromSdl( nameof(SDL_CreateGPUBuffer) );
	}

	/// <summary>
	/// Creates a buffer holding <paramref name="data"/>. The upload is submitted at once, before any frame recorded
	/// afterwards, so later draws always see the complete contents.
	/// </summary>
	public static GpuBuffer Create<T>( GpuDevice device, GpuBufferUsage usage, ReadOnlySpan<T> data ) where T : unmanaged
	{
		var bytes = MemoryMarshal.AsBytes( data );
		if ( bytes.IsEmpty ) throw new ArgumentException( "A GPU buffer needs data.", nameof(data) );
		var buffer = new GpuBuffer( device, usage, (uint)bytes.Length );
		try { buffer.Upload( bytes ); }
		catch { buffer.Dispose(); throw; }
		return buffer;
	}

	private void Upload( ReadOnlySpan<byte> bytes )
	{
		var transfer = (IntPtr)Transfer.Upload( device, bytes );
		var buffer = (IntPtr)Handle;
		try
		{
			device.Immediate( pass =>
			{
				var source = new SDL_GPUTransferBufferLocation { transfer_buffer = (SDL_GPUTransferBuffer*)transfer, offset = 0 };
				var destination = new SDL_GPUBufferRegion { buffer = (SDL_GPUBuffer*)buffer, offset = 0, size = Size };
				SDL_UploadToGPUBuffer( (SDL_GPUCopyPass*)pass, &source, &destination, false );
			}, wait: false );
		}
		finally { SDL_ReleaseGPUTransferBuffer( device.Handle, (SDL_GPUTransferBuffer*)transfer ); }
	}

	internal static SDL_GPUBufferUsageFlags ToSdl( GpuBufferUsage usage ) =>
		((usage & GpuBufferUsage.Vertex) != 0 ? SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX : 0)
		| ((usage & GpuBufferUsage.Index) != 0 ? SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX : 0);

	public void Dispose()
	{
		if ( Handle == null ) return;
		var handle = (IntPtr)Handle;
		Handle = null;
		device.Release( () => SDL_ReleaseGPUBuffer( device.Handle, (SDL_GPUBuffer*)handle ) );
	}
}

/// <summary>Short-lived transfer buffers for one-off copies. SDL keeps a released buffer alive until its copy completes.</summary>
internal static unsafe class Transfer
{
	public static SDL_GPUTransferBuffer* Upload( GpuDevice device, ReadOnlySpan<byte> bytes )
	{
		var transfer = Create( device, SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD, (uint)bytes.Length );
		var mapped = SDL_MapGPUTransferBuffer( device.Handle, transfer, false );
		if ( mapped == IntPtr.Zero )
		{
			SDL_ReleaseGPUTransferBuffer( device.Handle, transfer );
			throw GraphicsException.FromSdl( nameof(SDL_MapGPUTransferBuffer) );
		}
		bytes.CopyTo( new Span<byte>( (void*)mapped, bytes.Length ) );
		SDL_UnmapGPUTransferBuffer( device.Handle, transfer );
		return transfer;
	}

	public static SDL_GPUTransferBuffer* Create( GpuDevice device, SDL_GPUTransferBufferUsage usage, uint size )
	{
		var info = new SDL_GPUTransferBufferCreateInfo { usage = usage, size = size };
		var transfer = SDL_CreateGPUTransferBuffer( device.Handle, &info );
		return transfer != null ? transfer : throw GraphicsException.FromSdl( nameof(SDL_CreateGPUTransferBuffer) );
	}
}
