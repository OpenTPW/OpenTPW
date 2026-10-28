namespace OpenTPW;

public partial class TextureFile
{
	enum WctType : byte
	{
		ZlibPacked = 0x12,
		LzssInterleaved = 0x13
	}
}
