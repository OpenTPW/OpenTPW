#include "includes/sdl-gpu.hlsli"
struct BlitOutput
{
	float4 position : SV_Position;
	LOCATION(0) float2 texCoords : TEXCOORD0;
};
#ifdef STAGE_VERTEX
BlitOutput VSMain( uint vertexIndex : SV_VertexID )
{
	const float2 positions[3] = { float2( -1, -1 ), float2( -1, 3 ), float2( 3, -1 ) };
	const float2 texCoords[3] = { float2( 0, 1 ), float2( 0, -1 ), float2( 2, 1 ) };
	BlitOutput output;
	output.position = float4( positions[vertexIndex], 0, 1 );
	output.texCoords = texCoords[vertexIndex];
	return output;
}
#else
TEXTURE_SLOT( g_tInput, 0 );
float4 PSMain( BlitOutput input ) : SV_Target0
{
	return g_tInput.Sample( g_tInputSampler, input.texCoords );
}
#endif
