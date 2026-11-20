#include "includes/sdl-gpu.hlsli"
ConstantBuffer<ObjectUniforms> g_oUbo : UNIFORM_REGISTER;
struct ModelInput
{
	LOCATION(0) float3 position : TEXCOORD0;
	LOCATION(1) float3 color : TEXCOORD1;
	LOCATION(2) float2 texCoords : TEXCOORD2;
	LOCATION(3) int texIndex : TEXCOORD3;
	LOCATION(4) uint matFlags : TEXCOORD4;
};
struct ModelOutput
{
	float4 position : SV_Position;
	LOCATION(0) float2 vTexCoords : TEXCOORD0;
	LOCATION(1) float3 vColor : TEXCOORD1;
	LOCATION(2) float vDepth : TEXCOORD2;
	LOCATION(5) nointerpolation int texIndex : TEXCOORD5;
	LOCATION(6) nointerpolation uint outMatFlags : TEXCOORD6;
};
#ifdef STAGE_VERTEX
ModelOutput VSMain( ModelInput input )
{
	ModelOutput output;
	output.vTexCoords = float2( input.texCoords.x, 1.0 - input.texCoords.y );
	output.vColor = input.color;
	float4 pos = mul( g_oUbo.g_mModel, float4( input.position, 1.0 ) );
	float4 view = mul( g_oUbo.g_mView, pos );
	output.vDepth = abs( view.z );
	output.position = mul( g_oUbo.g_mProj, view );
	output.texIndex = input.texIndex;
	output.outMatFlags = input.matFlags;
	return output;
}
#else
#include "includes/model-textures.hlsli"
float4 PSMain( ModelOutput input ) : SV_Target0
{
	float4 sampleColor = SampleModel( input.texIndex, input.vTexCoords );
	if ( sampleColor.a < 0.1 ) discard;
	float4 result = float4( sampleColor.xyz * input.vColor, sampleColor.a );
	float fogFactor = clamp( (input.vDepth - 50.0) / (300.0 - 50.0), 0.0, 1.0 ) * g_oUbo.g_vFogColor.w;
	result.xyz = lerp( result.xyz, g_oUbo.g_vFogColor.xyz, fogFactor );
	return result;
}
#endif
