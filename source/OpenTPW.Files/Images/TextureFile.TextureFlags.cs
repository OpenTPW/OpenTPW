namespace OpenTPW;

public partial class TextureFile
{
	[Flags]
	enum TextureFlags
	{
		None = 0,

		Compressed = 0b10000,
		Unknown    = 0b00010,
		FullScale   = 0b00001
	}
}
