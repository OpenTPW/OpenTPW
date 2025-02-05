namespace OpenTPW;

public struct ResourceHandle<T> where T : Resource
{
	public T? _internal;

	public ResourceHandle( string path )
	{
		_internal = Resource.GetOrCreate<T>( path );
	}

	public T? Get()
	{
		return _internal;
	}

	public static ResourceHandle<T> From( T obj )
	{
		return new ResourceHandle<T>() { _internal = obj };
	}
}
