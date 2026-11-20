using SDL;
using static SDL.SDL3;

namespace OpenTPW.Graphics;

/// <summary>
/// Sampler kinds. The first six match OpenTPW's SamplerType in order and settings (Material.CreateSampler);
/// PointMinLinearMag is the screen-particle sampler of the retail D3D state (Render_SetD3dState, US 0x567620).
/// </summary>
public enum SamplerPreset
{
	/// <summary>Linear with 16x anisotropy, clamped.</summary>
	Anisotropic,
	/// <summary>Linear minification, magnification and mipmaps, clamped.</summary>
	Linear,
	/// <summary>Nearest everywhere, clamped.</summary>
	Point,
	/// <summary>Linear with 16x anisotropy, repeating.</summary>
	AnisotropicWrap,
	/// <summary>Linear with 16x anisotropy, mirrored repeat (Veldrid's Mirror, despite the name).</summary>
	AnisotropicRepeat,
	/// <summary>Nearest everywhere, repeating.</summary>
	PointWrap,
	/// <summary>Nearest minification and mipmaps, linear magnification, clamped.</summary>
	PointMinLinearMag,
}

/// <summary>A sampler object, created once per preset and owned by its device.</summary>
public sealed unsafe class GpuSampler
{
	private readonly GpuDevice device;
	internal SDL_GPUSampler* Handle { get; private set; }
	public SamplerPreset Preset { get; }

	internal GpuSampler( GpuDevice device, SamplerPreset preset )
	{
		this.device = device;
		Preset = preset;
		var info = Describe( preset );
		Handle = SDL_CreateGPUSampler( device.Handle, &info );
		if ( Handle == null ) throw GraphicsException.FromSdl( nameof(SDL_CreateGPUSampler) );
	}

	internal static SDL_GPUSamplerCreateInfo Describe( SamplerPreset preset )
	{
		const SDL_GPUFilter linear = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR, nearest = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST;
		var (min, mag, mip) = preset switch
		{
			SamplerPreset.Point or SamplerPreset.PointWrap => (nearest, nearest, SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST),
			SamplerPreset.PointMinLinearMag => (nearest, linear, SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST),
			_ => (linear, linear, SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_LINEAR),
		};
		var address = preset switch
		{
			SamplerPreset.AnisotropicWrap or SamplerPreset.PointWrap => SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT,
			SamplerPreset.AnisotropicRepeat => SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_MIRRORED_REPEAT,
			_ => SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
		};
		bool anisotropic = preset is SamplerPreset.Anisotropic or SamplerPreset.AnisotropicWrap or SamplerPreset.AnisotropicRepeat;
		return new SDL_GPUSamplerCreateInfo
		{
			min_filter = min,
			mag_filter = mag,
			mipmap_mode = mip,
			address_mode_u = address,
			address_mode_v = address,
			address_mode_w = address,
			enable_anisotropy = anisotropic,
			max_anisotropy = anisotropic ? 16 : 1,
			// The same LOD range as the Veldrid samplers.
			min_lod = 0,
			max_lod = 10
		};
	}

	internal void Destroy()
	{
		if ( Handle != null ) SDL_ReleaseGPUSampler( device.Handle, Handle );
		Handle = null;
	}
}
