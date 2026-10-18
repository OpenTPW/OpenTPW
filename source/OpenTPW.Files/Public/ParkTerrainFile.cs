using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>The MD2 heightfield and version-three LND material control map.</summary>
public sealed class ParkTerrainFile
{
	// Shared with ParkTerrainActor and terrain.shader's texture bindings.
	public const int CellRecordBytes = 4;
	public const int ColorChannels = 4;
	public const int ColorTextureCount = 6;
	public const int MaskTextureCount = 7;
	public const int BlendLookupWidth = 256;
	public const int BlendLookupRows = 5;
	private const int LandVersion = 3;
	private const int TileSize = 64;
	private const int LightingParameterBytes = BlendLookupRows * 2;
	private const int LandBitmapOffset = 1 + LightingParameterBytes;
	private const int BitmapHeaderBytes = 3 * sizeof( int );
	private const int LandPixelsOffset = LandBitmapOffset + BitmapHeaderBytes;
	private const int MaterialGroupHeaderBytes = sizeof( int ) + 1;

	// Defensive allocation limits, not dimensions required by the original format.
	private const int MaximumGridDimension = 2048;
	private const int MaximumGroundTextureDimension = 8192;
	private const int MaximumMaterialDimension = 2048;

	private static class HeightfieldLayout
	{
		public const uint ModelMagic = 0x1CD15D46;
		public const int ModelHeaderBytes = 0x70;
		public const int PointerOffset = 0x6c;
		// Offsets relative to the heightfield pointer, not the start of the MD2.
		public const int OriginX = 8;
		public const int OriginY = 12;
		public const int CellWidth = 16;
		public const int CellHeight = 20;
		public const int Width = 24;
		public const int Height = 28;
		public const int HeightsPointer = 40;
		public const int CellsPointer = 44;
	}

	[Flags]
	public enum CellFlags : byte
	{
		Hidden = 1,
		AlternateDiagonal = 4
	}

	public int Width { get; }
	public int Height { get; }
	public float OriginX { get; }
	public float OriginY { get; }
	public float CellWidth { get; }
	public float CellHeight { get; }
	public float[] Heights { get; }
	public byte[] Cells { get; }
	public int TextureWidth { get; }
	public int TextureHeight { get; }
	public byte[] GroundRgba { get; }
	public sealed record LandImage( int Width, int Height, byte[] Rgba );
	public List<LandImage> Materials { get; }
	public byte[] BlendLookup { get; }

	public ParkTerrainFile( byte[] model, byte[] land )
	{
		if ( model.Length < HeightfieldLayout.ModelHeaderBytes ||
			BinaryPrimitives.ReadUInt32LittleEndian( model ) != HeightfieldLayout.ModelMagic )
		{
			throw new InvalidDataException( "Invalid terrain MD2." );
		}

		int field = ReadOffset( model, HeightfieldLayout.PointerOffset );
		if ( field == 0 )
		{
			throw new InvalidDataException( "Terrain model has no heightfield." );
		}

		OriginX = ReadFloat( model, field + HeightfieldLayout.OriginX );
		OriginY = ReadFloat( model, field + HeightfieldLayout.OriginY );
		CellWidth = ReadFloat( model, field + HeightfieldLayout.CellWidth );
		CellHeight = ReadFloat( model, field + HeightfieldLayout.CellHeight );
		Width = ReadOffset( model, field + HeightfieldLayout.Width );
		Height = ReadOffset( model, field + HeightfieldLayout.Height );
		if ( Width <= 0 || Height <= 0 || Width > MaximumGridDimension || Height > MaximumGridDimension ||
			!float.IsFinite( CellWidth ) || !float.IsFinite( CellHeight ) || CellWidth <= 0 || CellHeight <= 0 ||
			!float.IsFinite( OriginX ) || !float.IsFinite( OriginY ) )
		{
			throw new InvalidDataException( "Invalid heightfield dimensions." );
		}

		int heightsOffset = ReadOffset( model, field + HeightfieldLayout.HeightsPointer );
		int cellsOffset = ReadOffset( model, field + HeightfieldLayout.CellsPointer );
		// Each cell has four corners, with shared vertices at the grid edges.
		Heights = ReadHeights( model, heightsOffset, checked( (Width + 1) * (Height + 1) ) );
		int cellBytes = checked( Width * Height * CellRecordBytes );
		if ( cellsOffset > model.Length - cellBytes )
		{
			throw new InvalidDataException( "Truncated terrain cells." );
		}

		Cells = model.AsSpan( cellsOffset, cellBytes ).ToArray();

		// Original routine 0x561022 reads version 3 and ten lighting bytes;
		// 0x561237 reads the ARGB material control bitmap that follows.
		if ( land.Length < LandPixelsOffset || land[0] != LandVersion )
		{
			throw new InvalidDataException( "Unsupported LND version." );
		}

		TextureWidth = ReadInt32( land, LandBitmapOffset );
		TextureHeight = ReadInt32( land, LandBitmapOffset + sizeof( int ) );
		int channels = ReadInt32( land, LandBitmapOffset + 2 * sizeof( int ) );
		if ( TextureWidth <= 0 || TextureHeight <= 0 ||
			TextureWidth > MaximumGroundTextureDimension || TextureHeight > MaximumGroundTextureDimension ||
			channels != ColorChannels )
		{
			throw new InvalidDataException( "Invalid LND ground bitmap." );
		}

		int pixelBytes = checked( TextureWidth * TextureHeight * ColorChannels );
		if ( land.Length - LandPixelsOffset < pixelBytes )
		{
			throw new InvalidDataException( "Truncated LND ground bitmap." );
		}

		GroundRgba = DecodeArgb( land, LandPixelsOffset, pixelBytes );
		Materials = ReadMaterials( land, LandPixelsOffset + pixelBytes );
		BlendLookup = CreateBlendLookup( land );
	}

	private static float[] ReadHeights( byte[] model, int offset, int count )
	{
		var heights = new float[count];
		for ( int i = 0; i < count; i++ )
		{
			heights[i] = ReadFloat( model, checked( offset + i * sizeof( float ) ) );
			if ( !float.IsFinite( heights[i] ) )
			{
				throw new InvalidDataException( "Invalid terrain height." );
			}
		}

		return heights;
	}

	private static byte[] DecodeArgb( byte[] source, int offset, int byteCount )
	{
		var rgba = new byte[byteCount];
		for ( int i = 0; i < byteCount; i += ColorChannels )
		{
			rgba[i] = source[offset + i + 1];
			rgba[i + 1] = source[offset + i + 2];
			rgba[i + 2] = source[offset + i + 3];
			// Preserve transparent holes occupied by the original scenery model.
			rgba[i + 3] = source[offset + i];
		}

		return rgba;
	}

	private static List<LandImage> ReadMaterials( byte[] land, int cursor )
	{
		// 0x56125e reads three mip groups. We use the first (full-resolution)
		// group's six colour textures and seven grayscale blend masks.
		if ( land.Length - cursor < MaterialGroupHeaderBytes )
		{
			throw new InvalidDataException( "Missing LND material group." );
		}

		int tilePixels = ReadInt32( land, cursor );
		cursor += MaterialGroupHeaderBytes; // Tile size word plus an unused byte.
		if ( tilePixels != TileSize )
		{
			throw new InvalidDataException( "Unsupported LND tile size." );
		}

		var materials = new List<LandImage>();
		for ( int image = 0; image < ColorTextureCount + MaskTextureCount; image++ )
		{
			materials.Add( ReadMaterialImage( land, ref cursor ) );
		}

		return materials;
	}

	private static LandImage ReadMaterialImage( byte[] land, ref int cursor )
	{
		int width = ReadInt32( land, cursor );
		int height = ReadInt32( land, cursor + sizeof( int ) );
		int channels = ReadInt32( land, cursor + 2 * sizeof( int ) );
		cursor += BitmapHeaderBytes;
		if ( width <= 0 || height <= 0 || width > MaximumMaterialDimension || height > MaximumMaterialDimension ||
			(channels != 1 && channels != ColorChannels) )
		{
			throw new InvalidDataException( "Invalid LND material bitmap." );
		}

		int pixelCount = checked( width * height );
		int byteCount = checked( pixelCount * channels );
		if ( cursor > land.Length - byteCount )
		{
			throw new InvalidDataException( "Truncated LND material bitmap." );
		}

		var rgba = new byte[pixelCount * ColorChannels];
		for ( int i = 0; i < pixelCount; i++ )
		{
			// Colour images are ARGB; single-channel masks repeat into RGB.
			for ( int channel = 0; channel < 3; channel++ )
			{
				int sourceChannel = channels == ColorChannels ? channel + 1 : 0;
				rgba[i * ColorChannels + channel] = land[cursor + i * channels + sourceChannel];
			}

			rgba[i * ColorChannels + 3] = byte.MaxValue;
		}

		cursor += byteCount;
		return new LandImage( width, height, rgba );
	}

	private static byte[] CreateBlendLookup( byte[] land )
	{
		var lookup = new byte[BlendLookupWidth * BlendLookupRows * ColorChannels];
		for ( int row = 0; row < BlendLookupRows; row++ )
		{
			// Each lighting pair gives a ramp centre and a span stored minus one.
			int center = land[1 + row * 2];
			int span = land[2 + row * 2] + 1;
			int low = center - span / 2;
			for ( int value = 0; value < BlendLookupWidth; value++ )
			{
				byte weight = (byte)Math.Clamp( (value - low) * byte.MaxValue / span, 0, byte.MaxValue );
				int pixel = (row * BlendLookupWidth + value) * ColorChannels;
				lookup[pixel] = lookup[pixel + 1] = lookup[pixel + 2] = weight;
				lookup[pixel + 3] = byte.MaxValue;
			}
		}

		return lookup;
	}

	private static int ReadInt32( byte[] bytes, int offset )
	{
		if ( offset < 0 || offset > bytes.Length - sizeof( int ) )
		{
			throw new InvalidDataException( "Truncated terrain data." );
		}

		return BinaryPrimitives.ReadInt32LittleEndian( bytes.AsSpan( offset, sizeof( int ) ) );
	}

	private static int ReadOffset( byte[] bytes, int offset ) => checked( (int)(uint)ReadInt32( bytes, offset ) );
	private static float ReadFloat( byte[] bytes, int offset ) => BitConverter.Int32BitsToSingle( ReadInt32( bytes, offset ) );

	public CellFlags GetCellFlags( int x, int y ) => (CellFlags)Cells[(y * Width + x) * CellRecordBytes];

	public Vector3 Position( int x, int y ) =>
		new( OriginX + x * CellWidth, OriginY + y * CellHeight, Heights[y * (Width + 1) + x] );
}
