namespace OpenTPW;

public class ResourceContext : IDisposable
{
	public static Stack<ResourceContext> Stack { get; } = new();
	public ResourceBinding Resources { get; init; } = new();

	public ResourceContext()
	{
		Stack.Push( this );
	}

	public void Set<T>( string name, T obj ) where T : unmanaged => Resources.Set( name, obj );
	public void Set( string name, Texture[] textures ) => Resources.Set( name, textures );
	public void Set( string name, Texture texture ) => Resources.Set( name, texture );

	public void Dispose()
	{
		Resources.Dispose();
		Stack.Pop();
	}
}
