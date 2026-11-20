using OpenTPW.Graphics;

namespace OpenTPW;

/// <summary>Game material flags mapped to the shared SDL GPU mesh render state.</summary>
[Flags]
public enum MaterialFlags
{
	None = 0, DisableDepthTest = 1, DisableDepthWrite = 2, DisableDepth = 3,
	DisableCulling = 4, Additive = 8, InvertDestination = 16
}

/// <summary>Game-facing named bindings; shared MeshMaterial owns the GPU pipeline and per-draw snapshots.</summary>
public partial class Material : Asset
{
	public Shader Shader { get; }
	public Type UniformBufferType { get; } = typeof(ObjectUniformBuffer);
	internal MeshMaterial Native { get; }
	public Material( string shaderPath, MaterialFlags flags = MaterialFlags.None ) : this( new Shader( shaderPath ), typeof(ObjectUniformBuffer), flags ) { }
	protected Material( string path, Type uniforms, MaterialFlags flags ) : this( new Shader( path ), uniforms, flags ) { }
	protected Material( Shader shader, Type uniforms, MaterialFlags flags )
	{
		Shader = shader;
		UniformBufferType = uniforms;
		Native = new MeshMaterial( Device, shader.Program, Vertex.Layout, new MeshRenderState
		{
			Blend = BlendState( flags ), DepthTest = !flags.HasFlag( MaterialFlags.DisableDepthTest ),
			DepthWrite = !flags.HasFlag( MaterialFlags.DisableDepthWrite ),
			Cull = flags.HasFlag( MaterialFlags.DisableCulling ) ? GpuCull.None : GpuCull.Back
		} );
		Native.Prepare( Render.Target );
		All.Add( this );
	}
	public void Set<T>( string name, T value ) where T : unmanaged
	{
		if ( name != "ObjectUniformBuffer" ) throw new ArgumentException( $"Unknown uniform binding: {name}" );
		var bytes = GpuUniforms.Bytes( in value );
		Native.SetUniforms( Shader.VertexUniform ? bytes : default, Shader.FragmentUniform ? bytes : default );
		Render.BufferUploads++;
	}
	public void Set( string name, Texture texture, SamplerType? sampler = null )
	{
		if ( name != "Color" || Shader.SamplerCount != 1 ) throw new ArgumentException( $"Shader {Shader.Path} needs a Color texture array." );
		Native.SetTexture( texture.NativeTexture, Device.GetSampler( (SamplerPreset)(sampler ?? texture.SamplerType) ) );
	}
	public void Set( string name, Texture[] textures, SamplerType sampler = SamplerType.AnisotropicWrap )
	{
		if ( name != "Color" || textures.Length != Shader.SamplerCount ) throw new ArgumentException( $"Shader {Shader.Path} needs {Shader.SamplerCount} Color textures, got {textures.Length}." );
		var gpuSampler = Device.GetSampler( (SamplerPreset)sampler );
		Native.SetTextures( textures.Select( t => new TextureBinding( t.NativeTexture, gpuSampler ) ).ToArray() );
	}
	internal static GpuBlend BlendState( MaterialFlags flags ) => flags.HasFlag( MaterialFlags.InvertDestination ) ? GpuBlend.InvertDestination
		: flags.HasFlag( MaterialFlags.Additive ) ? GpuBlend.Additive : GpuBlend.Alpha;
}

/// <summary>A material with the game's expected uniform struct type.</summary>
public class Material<T> : Material
{
	public Material( string path, MaterialFlags flags = MaterialFlags.None ) : base( path, typeof(T), flags ) { }
	public Material( Shader shader, MaterialFlags flags = MaterialFlags.None ) : base( shader, typeof(T), flags ) { }
}
