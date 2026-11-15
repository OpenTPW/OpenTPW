namespace OpenTPW;

public sealed class StringFile : BaseFormat
{
	private readonly BFMUReader? lookup;
	public string[] Entries { get; private set; } = [];

	public StringFile( string path )
	{
		ReadFromFile( path );
	}

	public StringFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	/// <summary>Reads a table with its language's explicit code page, independent of the global data mount.</summary>
	public StringFile( Stream stream, BFMUReader lookup )
	{
		this.lookup = lookup;
		ReadFromStream( stream );
	}

	public string this[int val] => Entries[val];

	protected override void ReadFromStream( Stream stream )
	{
		var reader = new BFSTReader( stream, lookup );
		Entries = reader.ReadFile();
	}
}
