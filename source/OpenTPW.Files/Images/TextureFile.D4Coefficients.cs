namespace OpenTPW;

public partial class TextureFile
{
	struct D4Coefficients
	{
		public float H0;
		public float H1;
		public float H2;
		public float H3;

		public float G0;
		public float G1;
		public float G2;
		public float G3;

		public float IH0;
		public float IH1;
		public float IH2;
		public float IH3;

		public float IG0;
		public float IG1;
		public float IG2;
		public float IG3;

		public D4Coefficients( float scale = 1.0f )
		{
			double s3 = Math.Sqrt( 3.0 );
			double denom = 4 * Math.Sqrt( 2.0 );

			H0 = (float)((1 + s3) / denom) * scale;
			H1 = (float)((3 + s3) / denom) * scale;
			H2 = (float)((3 - s3) / denom) * scale;
			H3 = (float)((1 - s3) / denom) * scale;

			G0 = H3;
			G1 = -H2;
			G2 = H1;
			G3 = -H0;

			IH0 = H2;
			IH1 = G2;
			IH2 = H0;
			IH3 = G0;

			IG0 = H3;
			IG1 = G3;
			IG2 = H1;
			IG3 = G1;
		}
	}
}
