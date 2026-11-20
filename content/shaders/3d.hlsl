#include "includes/sdl-gpu.hlsli"
ConstantBuffer<LegacyObjectUniforms> g_oUbo : UNIFORM_REGISTER;
#include "includes/3d.hlsli"

#ifdef STAGE_FRAGMENT
TEXTURE_SLOT( Color, 0 );

float4 PSMain( MeshOutput input ) : SV_Target0
{
	float3 lightDir = normalize( g_oUbo.g_vLightPos - input.vPosition );
	float3 lambert = dot( normalize( lightDir ), normalize( input.vNormal ) ) * g_oUbo.g_vLightColor;
	float3 ambient = g_oUbo.g_vLightColor * 0.2;
	// The original computes a specular term but does not include it in the result.
	float4 color = Color.Sample( ColorSampler, input.vTexCoords ) * 0.75;
	color.z *= 1.25;
	float3 fogColor = float3( 0.28, 0.88, 1.0 );
	float fogEnd = 30.0;
	float fogAmount = (1.0 - exp( -length( input.vPositionWs ) * 0.01 )) / (1.0 - exp( -fogEnd * 0.01 ));
	fogAmount = clamp( fogAmount, 0.0, 1.0 );
	float4 result = float4( lambert + ambient, 1.0 ) * color;
	result = lerp( result, float4( fogColor, 1.0 ), fogAmount );
	if ( result.w < 0.1 ) discard;
	return result;
}
#endif
