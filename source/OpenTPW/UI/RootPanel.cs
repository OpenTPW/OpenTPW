using Veldrid;

namespace OpenTPW.UI;

public class RootPanel : Thing
{
	public List<Panel> Children { get; set; } = new();
	public static RootPanel? Instance { get; set; }

	public RootPanel()
	{
		Instance ??= this;
	}

	protected override void OnRenderUI()
	{
		Children.ForEach( child => child.Draw() );
	}

	protected override void OnUpdate()
	{
		Children.ForEach( child => child.Update() );
	}

	public T AddChild<T>( T? obj = null ) where T : Panel
	{
		if ( obj is null )
			obj = Activator.CreateInstance( typeof( T ) ) as T;

		Children.Add( obj );

		return obj;
	}
}
