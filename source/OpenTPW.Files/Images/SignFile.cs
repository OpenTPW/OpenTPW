using System.Text;

namespace OpenTPW;

/// <summary>SGN v101 styles read by tp_nocd.exe at 005EC3A0.</summary>
public sealed class SignFile
{
	// The gate model uses two square texture slots, cut from one wide board.
	public const int TextureSize = 128;
	public const int BoardWidth = TextureSize * 2;
	public const int BoardHeight = TextureSize;
	public const int ColorChannels = 4;
	public const int GradientWidth = 16;
	public const int GradientHeight = 128;
	private const int Version = 101;
	private const int LineCount = 2;
	private const int FaceNameBytes = 64;
	private const int FontPathBytes = 260;
	private const int LogFontBytes = 60;
	private const int ShapeBytes = 44;
	private const int EffectBytes = 20;
	// Offsets within the original LOGFONT and SGN shape records.
	private const int FontWeightOffset = 16;
	private const int BevelOffset = 4;
	private const int BlurOffset = 8;

	public sealed record TextStyle( string Face, string FontFile, int Spacing, int Baseline,
		int Height, int Weight, float Bevel, float Blur, byte[] Gradient, byte[] Effect )
	{
		// An effect starts with RGBA colour, then two undecoded words and X/Y displacement.
		public int ShadowOffsetX => BitConverter.ToInt32( Effect, 12 );
		public int ShadowOffsetY => BitConverter.ToInt32( Effect, 16 );
	}
	public TextStyle[] Styles { get; }
	public TextureData Background { get; }

	public SignFile( byte[] bytes )
	{
		using var stream = new MemoryStream( bytes, false );
		using var reader = new BinaryReader( stream );
		if ( reader.ReadInt32() != Version )
		{
			throw new InvalidDataException( "Unsupported SGN version." );
		}

		_ = reader.ReadInt32(); // Reserved header word; its meaning is not yet known.
		bool hasBackground = reader.ReadByte() != 0;
		bool[] hasEffect = [reader.ReadInt32() != 0, reader.ReadInt32() != 0];
		Styles = new TextStyle[LineCount];
		for ( int i = 0; i < LineCount; i++ )
		{
			Styles[i] = ReadTextStyle( reader );
		}

		// Effects and ramps are stored after both font records, not inside each one.
		var effects = hasEffect.Select( present => present ? Read( reader, EffectBytes ) : new byte[EffectBytes] ).ToArray();
		for ( int i = 0; i < LineCount; i++ )
		{
			int width = reader.ReadInt32(), height = reader.ReadInt32(), channels = reader.ReadInt32();
			if ( width != GradientWidth || height != GradientHeight || channels != ColorChannels )
			{
				throw new InvalidDataException( "Unsupported SGN colour ramp." );
			}

			Styles[i] = Styles[i] with { Gradient = Read( reader, width * height * channels ), Effect = effects[i] };
		}

		Background = hasBackground ? ReadBackground( reader ) : new TextureData( BoardWidth, BoardHeight,
			new byte[BoardWidth * BoardHeight * ColorChannels] );
	}

	private static TextStyle ReadTextStyle( BinaryReader reader )
	{
		string face = Text( Read( reader, FaceNameBytes ) );
		string font = Text( Read( reader, FontPathBytes ) );
		int spacing = reader.ReadInt32(), baseline = reader.ReadInt32();
		byte[] logFont = Read( reader, LogFontBytes );
		byte[] shape = Read( reader, ShapeBytes );
		return new TextStyle( face, font, spacing, baseline, BitConverter.ToInt32( logFont ),
			BitConverter.ToInt32( logFont, FontWeightOffset ), BitConverter.ToSingle( shape, BevelOffset ),
			BitConverter.ToSingle( shape, BlurOffset ), [], [] );
	}

	private static TextureData ReadBackground( BinaryReader reader )
	{
		int width = reader.ReadInt32(), height = reader.ReadInt32(), channels = reader.ReadInt32();
		if ( width != BoardWidth || height != BoardHeight || channels != ColorChannels )
		{
			throw new InvalidDataException( "Unsupported SGN background." );
		}

		float luma = reader.ReadSingle(), blueChroma = reader.ReadSingle(), redChroma = reader.ReadSingle(), alpha = reader.ReadSingle();
		int colorSize = reader.ReadInt32(), alphaSize = reader.ReadInt32();
		var stream = reader.BaseStream;
		if ( colorSize <= 0 || alphaSize < 0 || (long)colorSize + alphaSize > stream.Length - stream.Position )
		{
			throw new InvalidDataException( "Invalid SGN background chunks." );
		}

		return TextureFile.DecodeSignBackground( width, height, luma, blueChroma, redChroma, alpha,
			Read( reader, colorSize ), Read( reader, alphaSize ) );
	}

	private static string Text( byte[] bytes ) => Encoding.Latin1.GetString( bytes ).Split( '\0' )[0];
	private static byte[] Read( BinaryReader reader, int count )
	{
		return reader.ReadExactBytes( count, "Truncated SGN file." );
	}
}
