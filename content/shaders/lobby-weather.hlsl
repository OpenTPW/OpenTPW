#include "includes/sdl-gpu.hlsli"
struct WeatherInput
{
	LOCATION(0) float3 position : TEXCOORD0;
	LOCATION(1) float3 color : TEXCOORD1;
	LOCATION(2) float2 texCoords : TEXCOORD2;
	LOCATION(3) int clipDepth : TEXCOORD3;
	LOCATION(4) uint opacity : TEXCOORD4;
};
struct WeatherOutput
{
	float4 position : SV_Position;
	LOCATION(0) float2 uv : TEXCOORD0;
	LOCATION(1) float4 tint : TEXCOORD1;
};
#ifdef STAGE_VERTEX
WeatherOutput VSMain( WeatherInput input )
{
	WeatherOutput output;
	float w = asfloat( input.clipDepth );
	output.position = float4( input.position.xy, input.position.z * w, w );
	output.uv = input.texCoords;
	output.tint = float4( input.color, float( input.opacity ) / 255.0 );
	return output;
}
#else
TEXTURE_SLOT( Color, 0 );
float4 PSMain( WeatherOutput input ) : SV_Target0
{
	return Color.Sample( ColorSampler, input.uv ) * input.tint;
}
#endif
