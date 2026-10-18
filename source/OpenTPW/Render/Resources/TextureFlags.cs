namespace OpenTPW;

[Flags]
public enum TextureFlags
{
	None,
	/// <summary>
		/// If this is set, then the pixel value (255, 0, 255) will be treated as a transparent pixel.
		/// </summary>
	    PinkChromaKey,
	/// <summary>
		/// Force point filtering for this texture's sampler
		/// </summary>
	    PointFilter,
	/// <summary>
		/// Force wrap for this texture's sampler
		/// </summary>
	    Wrap,
	Repeat,
	/// <summary>Linear MSDF data: clamp, bilinear filtering, one mip level.</summary>
	    DistanceField = 1 << 4
}
