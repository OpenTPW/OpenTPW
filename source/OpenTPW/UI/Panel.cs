namespace OpenTPW.UI;

public class Panel
{
	public Rectangle Bounds;

	public Panel()
	{
		Bounds = new( 0, 0, 0, 0 );
	}

	public void Update()
	{
		OnUpdate();
	}

	protected virtual void OnRender() { }
	protected virtual void OnUpdate() { }
	protected virtual void OnDelete() { }

	public void Draw()
	{
		OnRender();
	}

	public void Delete()
	{
		OnDelete();
		LobbyScene.Current.Hud.Children.Remove( this );
	}
}
