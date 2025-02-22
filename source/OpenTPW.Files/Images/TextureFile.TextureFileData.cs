namespace OpenTPW;

public partial class TextureFile
{
	struct TextureFileData
	{
		public TextureFlags Flags { get; set; }
		public bool HasAlphaChannel { get; set; }
		public byte BitsPerPixel { get; set; }
		public byte Version { get; set; }

		public short Width { get; set; }
		public short Height { get; set; }
		public short YChannelQuantizationScale { get; set; }
		public short CbChannelQuantizationScale { get; set; }
		public short CrChannelQuantizationScale { get; set; }
		public short AChannelQuantizationScale { get; set; }

		public int ColorChunkSize { get; set; }
		public int AlphaChunkSize { get; set; }

		public int Checksum { get; set; }

		public byte[] ColorChunk { get; set; }
		public byte[] AlphaChunk { get; set; }
	}
}
