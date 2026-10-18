namespace OpenTPW;

public static class ModelAnimationNames
{
	public static IEnumerable<(string Name, string Path)> FindCompanions( string modelPath, IEnumerable<string> files )
	{
		string stem = Path.GetFileNameWithoutExtension( modelPath.Replace( '\\', '/' ) );
		foreach ( string file in files )
		{
			if ( !Path.GetExtension( file ).Equals( ".md2", StringComparison.OrdinalIgnoreCase ) )
			{
				continue;
			}

			string candidate = Path.GetFileNameWithoutExtension( file.Replace( '\\', '/' ) );
			if ( !candidate.StartsWith( stem, StringComparison.OrdinalIgnoreCase ) || candidate.Length <= stem.Length )
			{
				continue;
			}

			string suffix = candidate[stem.Length..].ToLowerInvariant();
			// Shipped clips use a lifecycle letter followed by an optional variant number.
			if ( "cilsmeubrod".Contains( suffix[0] ) && suffix.Skip( 1 ).All( char.IsDigit ) )
			{
				yield return (suffix, file);
			}
		}
	}
}
