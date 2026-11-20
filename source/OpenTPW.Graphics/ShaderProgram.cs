using SDL;
using static SDL.SDL3;

namespace OpenTPW.Graphics;

/// <summary>Resource counts SDL needs for one shader stage; they are not recorded in the compiled code.</summary>
public readonly record struct ShaderResources( uint Samplers = 0, uint UniformBuffers = 0, uint StorageTextures = 0, uint StorageBuffers = 0 );

/// <summary>
/// A vertex and fragment shader pair loaded from precompiled DXIL or SPIR-V. File names follow
/// tools/build-shaders.ps1: <c>{name}.vert.dxil</c>, <c>{name}.frag.spv</c> and so on; entry points are VSMain and PSMain.
/// </summary>
public sealed unsafe class ShaderProgram : IDisposable
{
	private readonly GpuDevice device;
	internal SDL_GPUShader* Vertex { get; private set; }
	internal SDL_GPUShader* Fragment { get; private set; }
	public string Name { get; }

	private ShaderProgram( GpuDevice device, string name, SDL_GPUShader* vertex, SDL_GPUShader* fragment )
	{
		this.device = device;
		Name = name;
		Vertex = vertex;
		Fragment = fragment;
	}

	public static ShaderProgram Load( GpuDevice device, string directory, string name, ShaderResources vertexResources, ShaderResources fragmentResources )
	{
		string extension = device.ShaderFormat == SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL ? "dxil" : "spv";
		var vertexCode = File.ReadAllBytes( Path.Combine( directory, $"{name}.vert.{extension}" ) );
		var fragmentCode = File.ReadAllBytes( Path.Combine( directory, $"{name}.frag.{extension}" ) );
		return FromCode( device, name, vertexCode, vertexResources, fragmentCode, fragmentResources );
	}

	/// <summary>Creates the pair from code already in the device's <see cref="GpuDevice.ShaderFormat"/>.</summary>
	public static ShaderProgram FromCode( GpuDevice device, string name, ReadOnlySpan<byte> vertexCode, ShaderResources vertexResources,
		ReadOnlySpan<byte> fragmentCode, ShaderResources fragmentResources )
	{
		var vertex = Create( device, vertexCode, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, "VSMain"u8, vertexResources, name );
		try
		{
			var fragment = Create( device, fragmentCode, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, "PSMain"u8, fragmentResources, name );
			return new ShaderProgram( device, name, vertex, fragment );
		}
		catch { SDL_ReleaseGPUShader( device.Handle, vertex ); throw; }
	}

	private static SDL_GPUShader* Create( GpuDevice device, ReadOnlySpan<byte> code, SDL_GPUShaderStage stage, ReadOnlySpan<byte> entry,
		ShaderResources resources, string name )
	{
		if ( code.IsEmpty ) throw new ArgumentException( $"Shader {name} has no code.", nameof(code) );
		Span<byte> entryPoint = stackalloc byte[entry.Length + 1];
		entry.CopyTo( entryPoint );
		fixed ( byte* codePointer = code )
		fixed ( byte* entryPointer = entryPoint )
		{
			var info = new SDL_GPUShaderCreateInfo
			{
				code = codePointer,
				code_size = (nuint)code.Length,
				entrypoint = entryPointer,
				format = device.ShaderFormat,
				stage = stage,
				num_samplers = resources.Samplers,
				num_uniform_buffers = resources.UniformBuffers,
				num_storage_textures = resources.StorageTextures,
				num_storage_buffers = resources.StorageBuffers
			};
			var shader = SDL_CreateGPUShader( device.Handle, &info );
			return shader != null ? shader : throw GraphicsException.FromSdl( $"SDL_CreateGPUShader ({name} {stage})" );
		}
	}

	/// <summary>Pipelines keep their own reference to the shaders, so a program can be released after pipeline creation.</summary>
	public void Dispose()
	{
		if ( Vertex == null ) return;
		var (vertex, fragment) = ((IntPtr)Vertex, (IntPtr)Fragment);
		Vertex = Fragment = null;
		device.Release( () =>
		{
			SDL_ReleaseGPUShader( device.Handle, (SDL_GPUShader*)vertex );
			SDL_ReleaseGPUShader( device.Handle, (SDL_GPUShader*)fragment );
		} );
	}
}
