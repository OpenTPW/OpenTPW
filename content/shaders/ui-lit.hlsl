#include "includes/sdl-gpu.hlsli"
struct UiLitInput
{
	LOCATION(0) float3 position : TEXCOORD0;
	LOCATION(1) float3 color : TEXCOORD1;
	LOCATION(2) float2 texCoords : TEXCOORD2;
};
struct UiLitOutput
{
	float4 position : SV_Position;
	LOCATION(0) float2 texCoords : TEXCOORD0;
	LOCATION(1) float3 color : TEXCOORD1;
};
#ifdef STAGE_VERTEX
UiLitOutput VSMain( UiLitInput input )
{
	UiLitOutput output;
	output.texCoords = input.texCoords;
	output.color = input.color;
	// The advisor is drawn with the depth buffer (retail keeps ZENABLE and ZWRITEENABLE for model primitives), so its
	// screen-space depth passes through; the model is flattened to a thin slab around 0.2, in front of the scene.
	output.position = float4( input.position, 1 );
	return output;
}
#else
TEXTURE_SLOT( Color, 0 );
float4 PSMain( UiLitOutput input ) : SV_Target0
{
	float4 result = Color.Sample( ColorSampler, float2( input.texCoords.x, 1 - input.texCoords.y ) );
	result.rgb *= input.color;
	if ( result.a == 0 ) discard;
	return result;
}
#endif
