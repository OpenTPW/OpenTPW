namespace OpenTPW;

public class Resource
{
	public string Path { get; set; } = null!;
	public static List<Resource> All { get; private set; } = [];

	public static T GetOrCreate<T>( string path ) where T : Resource
	{
		return Get<T>( path ) ?? Create<T>( path );
	}

	public static T? Get<T>( string path ) where T : Resource
	{
		foreach ( var asset in All.ToArray() )
		{
			if ( asset.Path == path && asset.GetType() == typeof( T ) )
			{
				return (asset as T)!;
			}
		}

		return null;
	}

	public static T Create<T>( string path ) where T : Resource
	{
		return (Activator.CreateInstance( typeof( T ), path ) as T)!;
	}
}
