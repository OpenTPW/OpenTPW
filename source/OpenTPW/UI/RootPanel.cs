using Veldrid;

namespace OpenTPW.UI;

public class RootPanel : Entity
{
	public List<Panel> Children { get; set; } = new();
	public Panel? Modal { get; set; }
	public static RootPanel? Instance { get; set; }

	public RootPanel()
	{
		Instance ??= this;
		Layer = RenderLayer.Ui;
	}

	protected override void OnRender()
	{
		Children.ForEach( child => child.Draw() );
	}

	protected override void OnUpdate()
	{
		// A modal owns input dispatch only. Scene audio and particle clocks run in Level.Update.
		if ( Modal != null ) Modal.Update();
		else Children.ForEach( child => child.Update() );
	}

	public T AddChild<T>( T? obj = null ) where T : Panel
	{
		if ( obj is null )
			obj = Activator.CreateInstance( typeof( T ) ) as T;

		Children.Add( obj );

		return obj;
	}
}
