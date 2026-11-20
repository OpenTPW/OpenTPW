#include "includes/sdl-gpu.hlsli"
ConstantBuffer<ObjectUniforms> g_oUbo : UNIFORM_REGISTER;
struct TestInput
{
	LOCATION(0) float3 position : TEXCOORD0;
	LOCATION(1) float3 normal : TEXCOORD1;
	LOCATION(2) float2 texCoords : TEXCOORD2;
	LOCATION(3) int texIndex : TEXCOORD3;
	LOCATION(4) uint matFlags : TEXCOORD4;
};
struct TestOutput
{
	float4 position : SV_Position;
	LOCATION(0) float2 vTexCoords : TEXCOORD0;
	LOCATION(1) float3 vNormal : TEXCOORD1;
	LOCATION(2) float3 vPosition : TEXCOORD2;
	LOCATION(3) float3 vWorldPosition : TEXCOORD3;
	LOCATION(5) nointerpolation int texIndex : TEXCOORD5;
	LOCATION(6) nointerpolation uint outMatFlags : TEXCOORD6;
};
#ifdef STAGE_VERTEX
// The upper 3x3 of transpose(inverse(m)) is its cofactor matrix / determinant.
// Use full 4x4 minors, preserving the GLSL result without assuming affine input.
float3x3 NormalMatrix( float4x4 m )
{
	float3x3 result;
	[unroll] for ( uint row = 0; row < 3; row++ )
	{
		[unroll] for ( uint col = 0; col < 3; col++ )
		{
			float3x3 minor;
			uint mr = 0;
			[unroll] for ( uint r = 0; r < 4; r++ )
			{
				if ( r == row ) continue;
				uint mc = 0;
				[unroll] for ( uint c = 0; c < 4; c++ )
				{
					if ( c == col ) continue;
					minor[mr][mc++] = m[r][c];
				}
				mr++;
			}
			result[row][col] = ((row + col) % 2 == 0 ? 1.0 : -1.0) * determinant( minor );
		}
	}
	return result / determinant( m );
}

TestOutput VSMain( TestInput input )
{
	TestOutput output;
	output.vTexCoords = float2( input.texCoords.x, 1.0 - input.texCoords.y );
	output.vNormal = mul( NormalMatrix( g_oUbo.g_mModel ), input.normal );
	float4 pos = mul( g_oUbo.g_mModel, float4( input.position, 1.0 ) );
	output.vPosition = pos.xyz;
	output.vWorldPosition = mul( g_oUbo.g_mView, pos ).xyz;
	output.position = mul( g_oUbo.g_mProj, mul( g_oUbo.g_mView, pos ) );
	output.texIndex = input.texIndex;
	output.outMatFlags = input.matFlags;
	return output;
}
#else
#include "includes/model-textures.hlsli"
static const uint FLAG_BASE = 1;
static const uint FLAG_TRANSPARENT = 2;
static const uint FLAG_UNK = 4;
static const uint FLAG_X_AXIS_ANIM = 8;
static const uint FLAG_Y_AXIS_ANIM = 16;
static const uint FLAG_GOURAUD_SHADED = 32;

float4 PSMain( TestOutput input ) : SV_Target0
{
	float3 n = normalize( input.vNormal );
	float3 l = normalize( g_oUbo.g_vLightPos - input.vPosition );
	float3 diffuse = max( dot( n, l ), 0.0 ) * g_oUbo.g_vLightColor;
	float3 ambient = float3( 0.4, 0.4, 0.4 );
	float4 sampleColor = SampleModel( input.texIndex, input.vTexCoords );
	float3 shading = diffuse + ambient;
	float3 color;
	// Both branches are identical in the GLSL source; keep that behavior.
	if ( (input.outMatFlags & FLAG_GOURAUD_SHADED) != 0 ) color = sampleColor.xyz * shading;
	else color = sampleColor.xyz * shading;
	if ( sampleColor.a < 0.1 ) discard;
	float4 result = float4( color, sampleColor.a );
	float fogFactor = clamp( exp( length( input.vWorldPosition ) * 0.01 ) * 0.025, 0, 1 ) * g_oUbo.g_vFogColor.w;
	result.xyz = lerp( result.xyz, g_oUbo.g_vFogColor.xyz, fogFactor );
	return result;
}
#endif
