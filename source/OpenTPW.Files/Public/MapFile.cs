namespace OpenTPW;

public sealed class MapFile : BaseFormat
{
	// Offsets within the fixed TP2M/MAP header. AttributeBits describes tile
	// attributes; world cell spacing comes from the MD2 heightfield instead.
	private const int HeaderBytes = 80;
	private const int MapTagOffset = 36;
	private const int WidthOffset = 44;
	private const int HeightOffset = 48;
	private const int AttributeBitsOffset = 52;
	private const int MaximumDimension = 4096; // Defensive allocation bound.
	public int Width { get; private set; }
	public int Height { get; private set; }
	public int AttributeBits { get; private set; }
	public byte[] Tiles { get; private set; } = [];

	// Attribute values are preserved; simulation semantics are still incomplete.
	public enum TileType
	{
		Ground = 0,
		Wall = 1,
		River = 3,
		NotWalkable = 17,
		BrickPath = 144,
	}

	public MapFile( string path )
	{
		ReadFromFile( path );
	}

	public MapFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	protected override void ReadFromStream( Stream stream )
	{
		using var reader = new BinaryReader( stream );
		var header = reader.ReadExactBytes( HeaderBytes, "Invalid TP2M attribute map header." );
		if ( System.Text.Encoding.ASCII.GetString( header, 0, 4 ) != "TP2M" ||
			System.Text.Encoding.ASCII.GetString( header, MapTagOffset, 4 ) != "MAP " )
		{
			throw new InvalidDataException( "Invalid TP2M attribute map header." );
		}

		Width = BitConverter.ToInt32( header, WidthOffset );
		Height = BitConverter.ToInt32( header, HeightOffset );
		AttributeBits = BitConverter.ToInt32( header, AttributeBitsOffset );
		if ( Width <= 0 || Height <= 0 || Width > MaximumDimension || Height > MaximumDimension || AttributeBits <= 0 )
		{
			throw new InvalidDataException( "Invalid attribute map dimensions." );
		}

		int tileCount = checked( Width * Height );
		Tiles = reader.ReadExactBytes( tileCount, "Truncated attribute map." );
	}
}
