namespace OpenTPW;

/// <summary>
/// The twelve animation slots of a model, in the retail order of the slot letter table at
/// TP.exe 0x6fa67c (letters at 8-byte intervals). Slot 0xC means none
/// (TP.exe 0x471230).
/// </summary>
public enum AnimationSlot
{
	C = 0,
	D = 1,
	I = 2,
	L = 3,
	S = 4,
	M = 5,
	E = 6,
	U = 7,
	W = 8,
	B = 9,
	R = 10,
	O = 11
}

/// <summary>
/// Resolves model and animation file names the way the retail model loader does, so that every
/// caller uses one rule and a missing file is an error rather than a silent no-op.
/// <para>
/// Retail rule (TP.exe 0x460ac0, the loader that fills a model's slot tables): for each slot letter
/// it formats "%s%s%c%d.md2" (format string at 0x748d64) with the model path without its extension
/// and the number <c>segment + 1</c> (0x460e8a: the stored count plus one), and keeps going while the
/// file exists. Only if "&lt;model&gt;&lt;letter&gt;1.md2" is missing does it try the unnumbered
/// "%s%s%c.md2" (0x748d58; 0x460fd4..0x460fe0), which then becomes the slot's only segment.
/// Segment N of a slot is therefore "&lt;model&gt;&lt;letter&gt;N+1.md2", e.g. segment 0 of slot M is
/// "...M1.md2". Playback selects segments by this zero-based index (FUN_00471230 reads
/// <c>slotTable[index]</c>; lobby flocks call FUN_005d3430( 0, 1 ) = slot M, segment 0, looped).
/// </para>
/// <para>
/// The archives store names in mixed case (e.g. "Bfly_PINKm1.MD2"), so names are matched against the
/// directory listing without regard to case and the stored spelling is returned.
/// </para>
/// </summary>
public static class AnimationFiles
{
	private static BaseFileSystem? listedFileSystem;
	private static readonly Dictionary<string, Dictionary<string, string>> Listings = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>The letter used in file names for a slot.</summary>
	public static char Letter( AnimationSlot slot )
	{
		if ( !Enum.IsDefined( slot ) ) throw new ArgumentOutOfRangeException( nameof( slot ) );
		return slot.ToString()[0];
	}

	/// <summary>
	/// The model file as stored, matched without regard to case.
	/// </summary>
	/// <exception cref="FileNotFoundException">The model is not in its directory.</exception>
	public static string ResolveModel( string modelPath ) => ResolveFile( modelPath, "Model" );

	/// <summary>
	/// Any file as stored, matched without regard to case.
	/// </summary>
	/// <exception cref="FileNotFoundException">The file is not in its directory.</exception>
	public static string ResolveFile( string path, string kind = "File" )
	{
		var (directory, fileName) = Split( path );
		if ( Find( directory, fileName ) is string found )
			return found;
		throw new FileNotFoundException( $"{kind} not found: {FileSystem.GetAbsolutePath( path )}", path );
	}

	/// <summary>
	/// Every segment of a slot in retail order; empty when the model has no file for that slot.
	/// </summary>
	public static IReadOnlyList<string> Segments( string modelPath, AnimationSlot slot )
	{
		modelPath = ResolveModel( modelPath );
		var (directory, fileName) = Split( modelPath );
		string stem = Path.GetFileNameWithoutExtension( fileName );
		char letter = Letter( slot );
		var segments = new List<string>();
		for ( int number = 1; Find( directory, $"{stem}{letter}{number}.md2" ) is string numbered; number++ )
			segments.Add( numbered );
		if ( segments.Count == 0 && Find( directory, $"{stem}{letter}.md2" ) is string unnumbered )
			segments.Add( unnumbered );
		return segments;
	}

	/// <summary>
	/// The file of one zero-based segment of a slot.
	/// </summary>
	/// <exception cref="FileNotFoundException">The slot has no such segment; the message names the files looked for.</exception>
	public static string Resolve( string modelPath, AnimationSlot slot, int segment )
	{
		if ( segment < 0 ) throw new ArgumentOutOfRangeException( nameof( segment ) );
		var segments = Segments( modelPath, slot );
		if ( segment >= 0 && segment < segments.Count )
			return segments[segment];

		var (directory, fileName) = Split( modelPath );
		string stem = $"{directory}/{Path.GetFileNameWithoutExtension( fileName )}{Letter( slot )}";
		string looked = segment == 0
			? $"{FileSystem.GetAbsolutePath( $"{stem}1.md2" )} or {FileSystem.GetAbsolutePath( $"{stem}.md2" )}"
			: FileSystem.GetAbsolutePath( $"{stem}{segment + 1}.md2" );
		throw new FileNotFoundException(
			$"Animation slot {slot} segment {segment} of {modelPath} not found ({segments.Count} segment(s) present): {looked}",
			$"{stem}{segment + 1}.md2" );
	}

	private static (string Directory, string FileName) Split( string path )
	{
		path = path.Replace( '\\', '/' );
		int slash = path.LastIndexOf( '/' );
		return slash < 0 ? ("", path) : (path[..slash], path[(slash + 1)..]);
	}

	private static string? Find( string directory, string fileName )
	{
		var listing = Listing( directory );
		return listing.TryGetValue( fileName, out var stored ) ? stored : null;
	}

	/// <summary>
	/// File names in a directory (inside archives too), keyed without regard to case. The listing is
	/// used instead of <see cref="BaseFileSystem.FileExists"/>, which only sees loose files.
	/// </summary>
	private static Dictionary<string, string> Listing( string directory )
	{
		lock ( Listings )
		{
			if ( !ReferenceEquals( listedFileSystem, FileSystem ) )
			{
				Listings.Clear();
				listedFileSystem = FileSystem;
			}
			if ( Listings.TryGetValue( directory, out var listing ) )
				return listing;

			listing = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
			foreach ( var entry in FileSystem.GetFiles( directory ) )
			{
				string name = Path.GetFileName( entry.Replace( '\\', '/' ) );
				listing.TryAdd( name, directory.Length == 0 ? name : $"{directory}/{name}" );
			}
			Listings[directory] = listing;
			return listing;
		}
	}
}
