namespace OpenTPW;

public partial class StaticModelActor : Actor
{
	public Model? Model { get; set; }

	protected override void OnRender()
	{
		Model?.Draw( ModelMatrix, new Vector3( .25f, 0, 1 ), Vector3.One );
	}
}
