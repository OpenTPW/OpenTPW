#include "includes/sdl-gpu.hlsli"
struct UiOutput
{
	float4 position : SV_Position;
	LOCATION(0) float2 texCoords : TEXCOORD0;
};
#ifdef STAGE_VERTEX
UiOutput VSMain( MeshInput input )
{
	UiOutput output;
	output.texCoords = input.texCoords;
	output.position = float4( input.position.xy, 0, 1 );
	return output;
}
#else
TEXTURE_SLOT( Color, 0 );
float4 PSMain( UiOutput input ) : SV_Target0
{
	float4 result = Color.Sample( ColorSampler, float2( input.texCoords.x, 1 - input.texCoords.y ) );
	if ( result.a == 0 ) discard;
	return result;
}
#endif
