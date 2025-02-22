namespace OpenTPW;

public partial class TextureFile
{
	struct ImageDecodeState
	{
		public List<float> DequantizationBuffer;
		public List<float> RowDecodeBuffer;

		public ImageDecodeState( int size )
		{
			DequantizationBuffer = Enumerable.Repeat( 0f, size * size * size ).ToList();
			RowDecodeBuffer = Enumerable.Repeat( 0f, size * size * size ).ToList();
		}
	}
}
