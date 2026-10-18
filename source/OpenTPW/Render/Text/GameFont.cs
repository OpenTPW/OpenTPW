namespace OpenTPW;

/// <summary>Original English font resources; EmSize is pixels per em at the original resolution.</summary>
public sealed record GameFont( string ResourceName, string FaceFile, float EmSize, bool FromGameArchive = false )
{
	// Sizes were fitted to the original BF4 advances (see docs/FONT_RENDERING.md).
	// Fractions such as 40/3 retain the fitted size instead of rounding it to a
	// whole pixel; they are pixels per em, not point sizes or DPI conversions.
	public static GameFont MenuSmall { get; } = new( "MENUSMALL", "BIGLA___.TTF", 29, true );
	public static GameFont MenuMedium { get; } = new( "MENUMED", "BIGLA___.TTF", 37, true );
	public static GameFont MenuBig { get; } = new( "MENUBIG", "BIGLA___.TTF", 42, true );
	public static GameFont GameSmall { get; } = new( "GAME8AA", "comic.ttf", 32f / 3 );
	public static GameFont GameMedium { get; } = new( "GAME10AA", "comic.ttf", 40f / 3 );
	public static GameFont GameBig { get; } = new( "GAME12AA", "comic.ttf", 50f / 3 );
	public static GameFont GameBold { get; } = new( "GAMEBOLD12", "comicbd.ttf", 50f / 3 );
	public static GameFont TitleSmall { get; } = new( "TITLESMALL", "comic.ttf", 40f / 3 );
	public static GameFont TitleMedium { get; } = new( "TITLEMED", "comic.ttf", 52f / 3 );
	public static GameFont TitleBig { get; } = new( "TITLEBIG", "comic.ttf", 20 );
	public static GameFont CashSmall { get; } = new( "CASHSMALL", "BIGLA___.TTF", 37, true );
	public static GameFont CashMedium { get; } = new( "CASHMED", "BIGLA___.TTF", 48, true );
	public static GameFont CashBig { get; } = new( "CASHBIG", "BIGLA___.TTF", 64, true );
	public static GameFont SessionSmall { get; } = new( "SESHSMALL", "BIGLA___.TTF", 18, true );
	public static GameFont SessionMedium { get; } = new( "SESHMED", "BIGLA___.TTF", 24, true );
	public static GameFont SessionBig { get; } = new( "SESHBIG", "BIGLA___.TTF", 32, true );
	public static GameFont DateSmall { get; } = new( "DATESMALL", "comic.ttf", 32f / 3 );
	public static GameFont DateMedium { get; } = new( "DATEMED", "comic.ttf", 40f / 3 );
	public static GameFont DateBig { get; } = new( "DATEBIG", "comic.ttf", 50f / 3 );
	public static GameFont Console { get; } = new( "CONSOLE6", "cour.ttf", 25f / 3 );
}
