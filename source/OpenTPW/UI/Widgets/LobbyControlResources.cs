using System.Text;

namespace OpenTPW;

/// <summary>Ordinary UI models register their root node names, not their file names (0x48744c).</summary>
public static class LobbyControlResources
{
	public static uint NameHash( string name )
	{
		if ( name.Any( character => character > 255 ) ) throw new ArgumentException( "UI root names must be byte strings", nameof(name) );
		uint hash = 0;
		// 0x652f26 sign-extends each stored char before XOR and multiplication.
		foreach ( byte value in Encoding.Latin1.GetBytes( name ) ) hash = unchecked((hash ^ (uint)(sbyte)value) * 47);
		return hash;
	}

	public static string ResolveModel( uint hash )
	{
		var matches = new List<string>();
		foreach ( string path in FileSystem.GetFiles( "ui" ).Where( path => path.EndsWith( ".md2", StringComparison.OrdinalIgnoreCase ) ) )
		{
			byte[] bytes = FileSystem.ReadAllBytes( path );
			if ( bytes.Length < 0x7c ) throw new InvalidDataException( $"Truncated UI model: {path}" );
			uint root = BitConverter.ToUInt32( bytes, 0x78 );
			if ( root == 0 ) continue;
			if ( root > bytes.Length - 88 ) throw new InvalidDataException( $"Invalid UI model root: {path}" );
			uint start = BitConverter.ToUInt32( bytes, (int)root + 84 );
			if ( start >= bytes.Length ) throw new InvalidDataException( $"Invalid UI root name: {path}" );
			int end = Array.IndexOf( bytes, (byte)0, (int)start );
			if ( end < 0 ) throw new InvalidDataException( $"Unterminated UI root name: {path}" );
			string name = Encoding.Latin1.GetString( bytes, (int)start, end - (int)start );
			if ( NameHash( name ) == hash ) matches.Add( path );
		}
		return matches.Count == 1 ? matches[0] : throw new InvalidDataException( $"UI model root hash {hash:X8} has {matches.Count} matches" );
	}
}
