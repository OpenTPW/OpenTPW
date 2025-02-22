namespace OpenTPW;

internal record struct PreprocessedShader( string VertexShader, string FragmentShader );

internal static partial class ShaderPreprocessor
{
	public static PreprocessedShader PreprocessShader( string filePath )
	{
		var fileContents = File.ReadAllText( filePath );

		var segmentParser = new ShaderSegmentParser( fileContents );
		segmentParser.Parse( out var vertexStage1, out var fragmentStage1 );

		var result = new PreprocessedShader( vertexStage1, fragmentStage1 );
		return result;
	}
}
