#include "../includes/sdl-gpu.hlsli"

// Shader for the ModKit editor's ImGui draw data. It has no original GLSL: ImGui draws were never part of
// the retail renderer, so tools/verify-shaders.py checks this program's bindings on its own.
struct ImGuiInput
{
	LOCATION(0) float2 position : TEXCOORD0;
	LOCATION(1) float2 texCoords : TEXCOORD1;
	LOCATION(2) float4 color : TEXCOORD2;
};

struct ImGuiOutput
{
	float4 position : SV_Position;
	LOCATION(0) float2 texCoords : TEXCOORD0;
	LOCATION(1) float4 color : TEXCOORD1;
};

#ifdef STAGE_VERTEX
// xy: scale, zw: translation, taking ImGui's pixel coordinates to clip space.
cbuffer ImGuiUniforms : UNIFORM_REGISTER
{
	float4 g_vScaleTranslate;
};

ImGuiOutput VSMain( ImGuiInput input )
{
	ImGuiOutput output;
	output.position = float4( input.position * g_vScaleTranslate.xy + g_vScaleTranslate.zw, 0, 1 );
	output.texCoords = input.texCoords;
	output.color = input.color;
	return output;
}
#else
TEXTURE_SLOT( Color, 0 );
float4 PSMain( ImGuiOutput input ) : SV_Target0
{
	return input.color * Color.Sample( ColorSampler, input.texCoords );
}
#endif
