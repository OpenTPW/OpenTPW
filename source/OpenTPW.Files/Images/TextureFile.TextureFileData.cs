namespace OpenTPW;

public partial class TextureFile
{
	struct TextureFileData
	{
		public TextureFlags Flags { get; set; }
		public bool HasAlphaChannel { get; set; }
		public byte BitsPerPixel { get; set; }
		public byte Version { get; set; }
		public ushort Width { get; set; }
		public ushort Height { get; set; }
		public ushort YChannelQuantizationScale { get; set; }
		public ushort CbChannelQuantizationScale { get; set; }
		public ushort CrChannelQuantizationScale { get; set; }
		public ushort AChannelQuantizationScale { get; set; }
		public int ColorChunkSize { get; set; }
		public int AlphaChunkSize { get; set; }
		public int Checksum { get; set; }
		public byte[] ColorChunk { get; set; }
		public byte[] AlphaChunk { get; set; }
	}
}
