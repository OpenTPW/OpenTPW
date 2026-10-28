using System.Runtime.CompilerServices;
using Veldrid;

namespace OpenTPW;

/// <summary>
/// Frame ownership of GPU memory. Only one frame is in flight: the next frame starts after the
/// previous one's fence, so per-frame buffers, deferred deletions and textures updated with
/// <c>Device.UpdateTexture</c> are never rewritten while the GPU still reads them.
/// Per-draw data is written into host-visible buffers rented for the current frame; one shared
/// buffer per material would give every draw of that material the last value written.
/// </summary>
partial class Renderer
{
	private Fence? frameFence;
	private bool frameInFlight;
	private readonly Dictionary<(BufferUsage Usage, uint Size), List<DeviceBuffer>> framePools = new();
	private readonly Dictionary<(BufferUsage Usage, uint Size), int> framePoolUsed = new();

	/// <summary>Waits for the previous frame, then releases what it scheduled and recycles its buffers.</summary>
	private void BeginFrame()
	{
		frameFence ??= Device.ResourceFactory.CreateFence( false );
		if ( frameInFlight )
		{
			double waitBegin = Mark();
			Device.WaitForFence( frameFence );
			AddWait( Mark() - waitBegin );
			Device.ResetFence( frameFence );
			frameInFlight = false;
		}
		foreach ( var key in framePoolUsed.Keys.ToArray() ) framePoolUsed[key] = 0;
		ProcessDeletionQueue();
	}

	private void SubmitFrame( CommandList commands )
	{
		frameFence ??= Device.ResourceFactory.CreateFence( false );
		Device.SubmitCommands( commands, frameFence );
		frameInFlight = true;
	}

	/// <summary>A host-visible buffer of at least <paramref name="size"/> bytes, owned by the current frame.</summary>
	internal DeviceBuffer RentFrameBuffer( BufferUsage usage, uint size )
	{
		// Power-of-two sizes keep the pools few; uniform buffers keep the 2 KiB minimum the shaders were bound with.
		size = Math.Max( usage.HasFlag( BufferUsage.UniformBuffer ) ? 2048u : 256u, System.Numerics.BitOperations.RoundUpToPowerOf2( size ) );
		var key = (usage | BufferUsage.Dynamic, size);
		if ( !framePools.TryGetValue( key, out var pool ) ) framePools.Add( key, pool = new() );
		int used = framePoolUsed.GetValueOrDefault( key );
		if ( used == pool.Count ) pool.Add( Device.ResourceFactory.CreateBuffer( new BufferDescription( size, key.Item1 ) ) );
		framePoolUsed[key] = used + 1;
		return pool[used];
	}

	/// <summary>Copies <paramref name="data"/> into a host-visible buffer without a GPU submission.</summary>
	internal static unsafe void Write<T>( DeviceBuffer buffer, ReadOnlySpan<T> data ) where T : unmanaged
	{
		if ( (buffer.Usage & BufferUsage.Dynamic) == 0 ) throw new ArgumentException( "Only dynamic buffers are written directly", nameof(buffer) );
		if ( (ulong)data.Length * (ulong)Unsafe.SizeOf<T>() > buffer.SizeInBytes ) throw new ArgumentOutOfRangeException( nameof(data) );
		var mapped = Device.Map( buffer, MapMode.Write );
		try { data.CopyTo( new Span<T>( (void*)mapped.Data, data.Length ) ); }
		finally { Device.Unmap( buffer ); }
	}
}
