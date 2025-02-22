namespace OpenTPW;

partial class Material
{
	public static Material UIBasic = new( "content/shaders/ui.shader", MaterialFlags.DisableDepth );
	public static Material UIMesh = new Material<ObjectUniformBuffer>( "content/shaders/test_unlit.shader", MaterialFlags.DisableDepth );

	public static Material World = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
}
