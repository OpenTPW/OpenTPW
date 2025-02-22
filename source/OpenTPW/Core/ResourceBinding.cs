using System.Collections.ObjectModel;
using Veldrid;

namespace OpenTPW;

/// <summary>
/// Container for resource bindings
/// </summary>
public sealed class ResourceBinding : IDisposable
{
	private readonly Dictionary<string, BindableResource> _boundResources = new();
	private readonly DeviceBuffer _scratchBuffer;

	public ReadOnlyDictionary<string, BindableResource> BoundResources => _boundResources.AsReadOnly();

	public ResourceBinding()
	{
		var bufferDescription = new BufferDescription( 16 * 128, BufferUsage.UniformBuffer | BufferUsage.Dynamic );
		_scratchBuffer = Device.ResourceFactory.CreateBuffer( bufferDescription );
	}

	public void Set<T>( string name, T obj ) where T : unmanaged
	{
		var cmd = Render.GetCommandList();
		cmd.UpdateBuffer( _scratchBuffer, 0, [obj] );
		_boundResources[name] = _scratchBuffer;
	}

	public void Set( string name, Texture[] texture )
	{
		for ( int i = 0; i < texture.Length; i++ )
		{
			_boundResources[name + $"{i}"] = texture[i].NativeTexture;
		}

		_boundResources["s_" + name] = Sampler.Get( SamplerType.Default ).Get()!.VeldridSampler;
	}

	public void Set( string name, Texture texture )
	{
		_boundResources[name] = texture.NativeTexture;
		_boundResources["s_" + name] = Sampler.Get( texture.SamplerType ).Get()!.VeldridSampler;
	}

	public void Dispose()
	{
		Render.ScheduleDelete( () =>
		{
			_scratchBuffer.Dispose();

			if ( _boundResources.Count > 0 )
			{
				_boundResources.Clear();
			}
		} );
	}
}
