namespace OpenTPW;

public static class ModelTextureResolver
{
	/// <summary>Prefer the model's textures, then the park's shared texture archive.</summary>
	    public static string? FindPath( string modelPath, string textureName, Func<string, bool> fileExists )
	{
		string path = modelPath.Replace( '\\', '/' );
		int slash = path.LastIndexOf( '/' );
		string directory = slash < 0 ? "" : path[..(slash + 1)];
		string local = $"{directory}textures/{textureName}.wct";
		if ( fileExists( local ) )
		{
			return local;
		}

		var parts = path.Split( '/' );
		int level = Array.FindIndex( parts, p => p.Equals( "levels", StringComparison.OrdinalIgnoreCase ) );
		if ( level >= 0 && level + 2 < parts.Length )
		{
			string shared = $"{string.Join( "/", parts.Take( level + 2 ) )}/sharetex/{textureName}.wct";
			if ( fileExists( shared ) )
			{
				return shared;
			}
		}

		return null;
	}
}
