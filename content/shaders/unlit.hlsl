#include "includes/sdl-gpu.hlsli"
ConstantBuffer<LegacyObjectUniforms> g_oUbo : UNIFORM_REGISTER;

struct UnlitOutput
{
	float4 position : SV_Position;
	LOCATION(0) float2 vTexCoords : TEXCOORD0;
	LOCATION(1) float3 vNormal : TEXCOORD1;
	LOCATION(2) float3 vPosition : TEXCOORD2;
};

#ifdef STAGE_VERTEX
UnlitOutput VSMain( MeshInput input )
{
	UnlitOutput output;
	output.vTexCoords = input.texCoords;
	output.vNormal = input.normal;
	float4 pos = mul( g_oUbo.g_mModel, float4( input.position, 1.0 ) );
	output.vPosition = pos.xyz;
	output.position = mul( g_oUbo.g_mProj, mul( g_oUbo.g_mView, pos ) );
	return output;
}
#else
// The original fragment declares the object uniform but never reads it.
TEXTURE_SLOT( Color, 0 );
float4 PSMain( UnlitOutput input ) : SV_Target0
{
	return Color.Sample( ColorSampler, input.vTexCoords );
}
#endif
