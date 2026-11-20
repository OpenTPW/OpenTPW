#ifndef OPENTPW_SDL_GPU_HLSLI
#define OPENTPW_SDL_GPU_HLSLI

// SDL GPU uses TEXCOORDn semantics on D3D12 and numeric locations on Vulkan.
#ifdef SPIRV
#define LOCATION(index) [[vk::location(index)]]
#define COMBINED_SAMPLER [[vk::combinedImageSampler]]
#else
#define LOCATION(index)
#define COMBINED_SAMPLER
#endif

// Each SDL sampler slot is a texture/sampler pair. Vulkan combines the pair at
// the same set/binding; D3D12 has separate t/s registers at the same slot.
#define TEXTURE_SLOT(name, slot) \
	COMBINED_SAMPLER Texture2D<float4> name : register(t##slot, space2); \
	COMBINED_SAMPLER SamplerState name##Sampler : register(s##slot, space2)

#if defined(STAGE_VERTEX)
#define UNIFORM_REGISTER register(b0, space1)
#elif defined(STAGE_FRAGMENT)
#define UNIFORM_REGISTER register(b0, space3)
#else
#error Define STAGE_VERTEX or STAGE_FRAGMENT when compiling.
#endif

// Column-major matrices preserve the GLSL mat4 byte interpretation. Explicit
// padding keeps float3 members at the original std140 offsets on both backends.
struct ObjectUniforms
{
	column_major float4x4 g_mModel;
	column_major float4x4 g_mView;
	column_major float4x4 g_mProj;
	float3 g_vLightPos;
	float _padding0;
	float3 g_vLightColor;
	float _padding1;
	float3 g_vCameraPos;
	float g_flTime;
	float4 g_vFogColor;
};

struct LegacyObjectUniforms
{
	column_major float4x4 g_mModel;
	column_major float4x4 g_mView;
	column_major float4x4 g_mProj;
	float3 g_vLightPos;
	float _padding0;
	float3 g_vLightColor;
	float _padding1;
	float3 g_vCameraPos;
	float _padding2;
};

// water.shader calls the member at byte 236 g_fTime rather than g_flTime.
struct WaterUniforms
{
	column_major float4x4 g_mModel;
	column_major float4x4 g_mView;
	column_major float4x4 g_mProj;
	float3 g_vLightPos;
	float _padding0;
	float3 g_vLightColor;
	float _padding1;
	float3 g_vCameraPos;
	float g_fTime;
	float4 g_vFogColor;
};

struct MeshInput
{
	LOCATION(0) float3 position : TEXCOORD0;
	LOCATION(1) float3 normal : TEXCOORD1;
	LOCATION(2) float2 texCoords : TEXCOORD2;
};

#endif
