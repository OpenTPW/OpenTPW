using Veldrid;

namespace OpenTPW.UI;

public class RootPanel : Actor
{
	public List<Panel> Children { get; set; } = new();
	public static RootPanel? Instance { get; set; }

	public RootPanel()
	{
		Instance ??= this;
	}

	protected override void OnRenderUI()
	{
		Children.ToList().ForEach( child => child.Draw() );
	}

	protected override void OnUpdate()
	{
		var modal = Children.LastOrDefault( child => child.IsModal );
		if ( modal != null )
		{
			modal.Update();
			return;
		}

		foreach ( var child in Children.ToArray() )
		{
			child.Update();
			if ( Children.Any( panel => panel.IsModal ) )
			{
				break;
			}
		}
	}

	public T AddChild<T>( T? obj = null ) where T : Panel
	{
		if ( obj is null )
		{
			obj = Activator.CreateInstance( typeof( T ) ) as T;
		}

		obj!.Parent = this;
		Children.Add( obj );
		return obj;
	}

	public void ClearChildren()
	{
		foreach ( var child in Children.ToArray() )
		{
			child.Delete();
		}
	}
}
