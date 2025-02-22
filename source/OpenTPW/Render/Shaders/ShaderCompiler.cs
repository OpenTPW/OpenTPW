using System.Diagnostics;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;

namespace OpenTPW;

internal struct ShaderInfo
{
	public Veldrid.Shader VertexShader { get; set; }
	public Veldrid.Shader FragmentShader { get; set; }
	public SpirvReflection Reflection { get; set; }

	public readonly Veldrid.Shader[] ShaderProgram => [VertexShader, FragmentShader];
}

internal static class ShaderCompiler
{
	private static CrossCompileTarget GetCrossCompileTarget()
	{
		return Device.ResourceFactory.BackendType switch
		{
			GraphicsBackend.Direct3D11 => CrossCompileTarget.HLSL,
			GraphicsBackend.OpenGL => CrossCompileTarget.GLSL,
			GraphicsBackend.Vulkan => CrossCompileTarget.GLSL,
			GraphicsBackend.Metal => CrossCompileTarget.MSL,
			GraphicsBackend.OpenGLES => CrossCompileTarget.ESSL,
			_ => throw new NotImplementedException( $"Unknown cross-compile target" )
		};
	}

	private static byte[] GetBytes( string code )
	{
		return Device.ResourceFactory.BackendType switch
		{
			GraphicsBackend.Direct3D11 or GraphicsBackend.OpenGL or GraphicsBackend.OpenGLES or GraphicsBackend.Vulkan => Encoding.ASCII.GetBytes( code ),
			GraphicsBackend.Metal => Encoding.UTF8.GetBytes( code ),
			_ => throw new SpirvCompilationException( "Unknown target" ),
		};
	}

	internal static bool HasSpirvHeader( byte[] bytes )
	{
		return bytes.Length > 4
			&& bytes[0] == 0x03
			&& bytes[1] == 0x02
			&& bytes[2] == 0x23
			&& bytes[3] == 0x07;
	}

	public static ShaderInfo CompileShader( string path )
	{
		var target = GetCrossCompileTarget();

		var preprocessedShader = ShaderPreprocessor.PreprocessShader( path );
		var vertexSource = preprocessedShader.VertexShader;
		var fragmentSource = preprocessedShader.FragmentShader;

		var vertexSourceBytes = GetBytes( vertexSource );
		var fragmentSourceBytes = GetBytes( fragmentSource );
		var compilationResult = SpirvCompilation.CompileVertexFragment( vertexSourceBytes, fragmentSourceBytes, target );

		if ( Device.ResourceFactory.BackendType != GraphicsBackend.Vulkan )
		{
			vertexSource = compilationResult.VertexShader;
			fragmentSource = compilationResult.FragmentShader;
		}

		var vertexSpirv = SpirvCompilation.CompileGlslToSpirv( vertexSource, path, ShaderStages.Vertex, new() );
		var fragmentSpirv = SpirvCompilation.CompileGlslToSpirv( fragmentSource, path, ShaderStages.Fragment, new() );

		Debug.Assert( HasSpirvHeader( vertexSpirv.SpirvBytes ) );
		Debug.Assert( HasSpirvHeader( fragmentSpirv.SpirvBytes ) );

		var vertexShader = Device.ResourceFactory.CreateShader( new ShaderDescription( ShaderStages.Vertex, vertexSpirv.SpirvBytes, "main" ) );
		var fragmentShader = Device.ResourceFactory.CreateShader( new ShaderDescription( ShaderStages.Fragment, fragmentSpirv.SpirvBytes, "main" ) );

		return new ShaderInfo()
		{
			VertexShader = vertexShader,
			FragmentShader = fragmentShader,
			Reflection = compilationResult.Reflection
		};
	}
}
