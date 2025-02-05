using Veldrid;

namespace OpenTPW;

public class Sampler : Resource
{
	public Veldrid.Sampler VeldridSampler { get; private set; }

	public static SamplerHandle Anisotropic { get; private set; }
	public static SamplerHandle Linear { get; private set; }
	public static SamplerHandle Point { get; private set; }
	public static SamplerHandle AnisotropicWrap { get; private set; }
	public static SamplerHandle AnisotropicRepeat { get; private set; }

	public static SamplerHandle Get( SamplerType type ) => type switch
	{
		SamplerType.Anisotropic => Anisotropic,
		SamplerType.Linear => Linear,
		SamplerType.Point => Point,
		SamplerType.AnisotropicRepeat => AnisotropicRepeat,
		_ => AnisotropicWrap,
	};

	public static void CreateSamplers()
	{
		Anisotropic = SamplerHandle.From( new Sampler( SamplerType.Anisotropic ) );
		Linear = SamplerHandle.From( new Sampler( SamplerType.Linear ) );
		Point = SamplerHandle.From( new Sampler( SamplerType.Point ) );
		AnisotropicWrap = SamplerHandle.From( new Sampler( SamplerType.AnisotropicWrap ) );
		AnisotropicRepeat = SamplerHandle.From( new Sampler( SamplerType.AnisotropicRepeat ) );
	}

	private Sampler( SamplerType type )
	{
		var samplerFilter = type switch
		{
			SamplerType.Anisotropic or SamplerType.AnisotropicWrap or SamplerType.AnisotropicRepeat => SamplerFilter.Anisotropic,
			SamplerType.Linear => SamplerFilter.MinLinear_MagLinear_MipLinear,
			SamplerType.Point => SamplerFilter.MinPoint_MagPoint_MipPoint,
			_ => throw new NotImplementedException()
		};

		var samplerAddressMode = type switch
		{
			SamplerType.Anisotropic or SamplerType.Linear or SamplerType.Point => SamplerAddressMode.Clamp,
			SamplerType.AnisotropicWrap => SamplerAddressMode.Wrap,
			SamplerType.AnisotropicRepeat => SamplerAddressMode.Mirror,
			_ => throw new NotImplementedException()
		};

		var samplerDescription = new SamplerDescription(
			samplerAddressMode,
			samplerAddressMode,
			samplerAddressMode,
			samplerFilter,
			ComparisonKind.Always,
			(type == SamplerType.Anisotropic || type == SamplerType.AnisotropicWrap || type == SamplerType.AnisotropicRepeat) ? 16u : 0u,
			0,
			10,
			0,
			SamplerBorderColor.TransparentBlack
		);

		VeldridSampler = Device.ResourceFactory.CreateSampler( samplerDescription );
	}
}
