namespace OpenTPW;

public class SkyActor : StaticModelActor
{
	public SkyActor()
	{
		var texture = new Texture( [79, 214, 255, 255], 1, 1 );

		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		material.Set( "Color", texture );

		Model = new Model( Primitives.Cube.Create( material ) );
		Scale = new Vector3( -1900f );
	}
}
