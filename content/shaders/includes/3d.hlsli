// Shared vertex stage corresponding to includes/3d.vert. The final view-space
// output is present in the original shared stage even when a fragment ignores it.
struct MeshOutput
{
	float4 position : SV_Position;
	LOCATION(0) float2 vTexCoords : TEXCOORD0;
	LOCATION(1) float3 vNormal : TEXCOORD1;
	LOCATION(2) float3 vPosition : TEXCOORD2;
	LOCATION(3) float3 vPositionWs : TEXCOORD3;
	LOCATION(4) float3 vPositionVs : TEXCOORD4;
};

#ifdef STAGE_VERTEX
MeshOutput VSMain( MeshInput input )
{
	MeshOutput output;
	output.vTexCoords = input.texCoords;
	output.vNormal = input.normal;
	float4 pos = mul( g_oUbo.g_mModel, float4( input.position, 1.0 ) );
	output.vPosition = pos.xyz;
	output.vPositionVs = mul( g_oUbo.g_mView, pos ).xyz;
	output.vPositionWs = mul( g_oUbo.g_mProj, mul( g_oUbo.g_mView, pos ) ).xyz;
	output.position = mul( g_oUbo.g_mProj, mul( g_oUbo.g_mView, pos ) );
	return output;
}
#endif
