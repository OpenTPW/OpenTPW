namespace OpenTPW.UI;

public class Panel
{
	public Vector2 Position;

	internal RootPanel? Parent { get; set; }

	public virtual bool IsModal => false;

	protected bool CanReceiveInput => Parent == null || Parent.Children.LastOrDefault( p => p.IsModal ) is not
	{
	} modal || ReferenceEquals( modal, this );

	public Panel()
	{
		Position = Vector2.Zero;
	}

	public void Update()
	{
		OnUpdate();
	}

	protected virtual void OnRender()
	{
	}

	protected virtual void OnUpdate()
	{
	}

	protected virtual void OnDelete()
	{
	}

	public void Draw()
	{
		OnRender();
	}

	public void Delete()
	{
		OnDelete();
		Parent?.Children.Remove( this );
		Parent = null;
	}
}
