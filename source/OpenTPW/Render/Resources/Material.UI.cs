namespace OpenTPW;

partial class Material
{
	/// <summary>
	/// Default shader for 2D UI objects
	/// </summary>
	public static Material UI = new( "content/shaders/ui.shader", MaterialFlags.DisableDepth );

	/// <summary>
	/// Default shader for 3D objects
	/// </summary>
	public static Material Default = new Material<ObjectUniformBuffer>( "content/shaders/3d.shader" );

	/// <summary>
	/// Test shader
	/// </summary>
	public static Material Test = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );

	public static Material TestUnlit = new Material<ObjectUniformBuffer>( "content/shaders/test_unlit.shader", MaterialFlags.DisableDepth );
}
