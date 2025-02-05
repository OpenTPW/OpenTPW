namespace OpenTPW;

public class Water : ModelEntity
{
	public Water()
	{
		var texture = new Texture( "lobby/terrain/textures/jri_lak3.wct", TextureFlags.Wrap );

		var material = new Material<ObjectUniformBuffer>( "content/shaders/water.shader" );
		material.Set( "Color", texture );

		Model = new Model( Primitives.Plane.Create( material, new Point2( 100, 100 ) ) );
	}

	protected override void OnUpdate()
	{
		Scale = new Vector3( 10 );
	}
}
