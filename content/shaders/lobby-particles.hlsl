#include "includes/sdl-gpu.hlsli"
struct ParticleInput
{
	LOCATION(0) float3 position : TEXCOORD0;
	LOCATION(1) float3 normal : TEXCOORD1;
	LOCATION(2) float2 texCoords : TEXCOORD2;
	LOCATION(4) uint alpha : TEXCOORD4;
};
struct ParticleOutput
{
	float4 position : SV_Position;
	LOCATION(0) float2 texCoords : TEXCOORD0;
	LOCATION(1) float4 colour : TEXCOORD1;
};
#ifdef STAGE_VERTEX
ParticleOutput VSMain( ParticleInput input )
{
	ParticleOutput output;
	output.texCoords = input.texCoords;
	output.colour = float4( input.normal, float( input.alpha ) / 255.0 );
	output.position = float4( input.position.xy, 0, 1 );
	return output;
}
#else
TEXTURE_SLOT( Color, 0 );
float4 PSMain( ParticleOutput input ) : SV_Target0
{
	float4 result = Color.Sample( ColorSampler, float2( input.texCoords.x, 1 - input.texCoords.y ) ) * input.colour;
	if ( result.a == 0 ) discard;
	return result;
}
#endif
