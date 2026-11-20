using OpenTPW.Graphics;

namespace OpenTPW;

/// <summary>Precompiled SDL GPU shader pair; resource counts follow content/shaders/README.md.</summary>
public sealed class Shader : Asset
{
	private static readonly Dictionary<string, ShaderProgram> programs = new( StringComparer.Ordinal );
	public ShaderProgram Program { get; }
	public uint SamplerCount { get; }
	public bool VertexUniform { get; }
	public bool FragmentUniform { get; }
	internal Shader( string path )
	{
		Path = path;
		string name = System.IO.Path.GetFileNameWithoutExtension( path );
		VertexUniform = name is "3d" or "water" or "lobby-model" or "test" or "unlit" or "lobby-sky";
		FragmentUniform = name is "3d" or "water" or "lobby-model" or "test";
		SamplerCount = name is "lobby-model" or "test" ? 16u : 1u;
		if ( !programs.TryGetValue( name, out var program ) )
		{
			program = ShaderProgram.Load( Device, "content/shaders/compiled", name,
				new( UniformBuffers: VertexUniform ? 1u : 0u ), new( Samplers: SamplerCount, UniformBuffers: FragmentUniform ? 1u : 0u ) );
			programs.Add( name, program );
		}
		Program = program;
		All.Add( this );
	}
	internal static void ReleasePrograms() { foreach ( var program in programs.Values ) program.Dispose(); programs.Clear(); }
}
