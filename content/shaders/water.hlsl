#include "includes/sdl-gpu.hlsli"
ConstantBuffer<WaterUniforms> g_oUbo : UNIFORM_REGISTER;
#include "includes/3d.hlsli"

#ifdef STAGE_FRAGMENT
TEXTURE_SLOT( Color, 0 );
float4 PSMain( MeshOutput input ) : SV_Target0
{
	float uvScale = 0.05;
	float waveScale = 2.0 * uvScale;
	float waveFrequency = 0.25;
	float waveAmplitude = 0.025;
	float pi = 3.14159265359;
	float2 sinTime = (input.vPosition.yx * waveScale + g_oUbo.g_fTime * waveFrequency) * pi;
	float2 uv = input.vPosition.xy * uvScale + float2( sin( sinTime.x ), sin( sinTime.y ) ) * waveAmplitude;
	float2 offset = float2( 0, g_oUbo.g_fTime ) * 0.25;
	float4 result = Color.Sample( ColorSampler, uv + offset );
	result.xyz *= 0.4;
	float fogFactor = clamp( exp( length( input.vPositionVs ) * 0.01 ) * 0.025, 0, 1 );
	result.xyz = lerp( result.xyz, g_oUbo.g_vFogColor.xyz, fogFactor );
	return result;
}
#endif
