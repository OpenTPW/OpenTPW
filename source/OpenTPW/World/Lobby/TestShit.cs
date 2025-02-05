namespace OpenTPW;

public sealed class TestShit : UIModelEntity
{
	public TestShit( string modelName )
	{
		Model = new Model( modelName, Material.TestUnlit );
	}
}
