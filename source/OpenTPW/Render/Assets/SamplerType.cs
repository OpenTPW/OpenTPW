namespace OpenTPW;

public enum SamplerType
{
	Anisotropic,
	Linear,
	Point,

	AnisotropicWrap,
	AnisotropicRepeat,
	PointWrap,
	/// <summary>Render_SetD3dState (US 0x567620): screen sprites use point minification and linear magnification.</summary>
	PointMinLinearMag
}
