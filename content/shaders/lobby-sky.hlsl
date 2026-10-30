#include "includes/sdl-gpu.hlsli"
struct SkyInput
{
	LOCATION(0) float3 position : TEXCOORD0;
	LOCATION(1) float3 color : TEXCOORD1;
	LOCATION(2) float2 texCoords : TEXCOORD2;
	LOCATION(3) int texIndex : TEXCOORD3;
	LOCATION(4) uint opacity : TEXCOORD4;
};
struct SkyOutput
{
	float4 position : SV_Position;
	LOCATION(0) float2 uv : TEXCOORD0;
	LOCATION(1) float4 tint : TEXCOORD1;
};
#ifdef STAGE_VERTEX
ConstantBuffer<ObjectUniforms> g_oUbo : UNIFORM_REGISTER;
SkyOutput VSMain( SkyInput input )
{
	SkyOutput output;
	output.uv = input.texCoords;
	output.tint = float4( input.color, float( input.opacity ) / 255.0 );
	output.position = mul( g_oUbo.g_mProj, mul( g_oUbo.g_mView, mul( g_oUbo.g_mModel, float4( input.position, 1.0 ) ) ) );
	return output;
}
#else
TEXTURE_SLOT( Color, 0 );
float4 PSMain( SkyOutput input ) : SV_Target0
{
	return Color.Sample( ColorSampler, input.uv ) * input.tint;
}
#endif
