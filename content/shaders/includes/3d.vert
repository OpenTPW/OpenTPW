//
// Layout
//
layout( location = 0 ) in vec3 position;
layout( location = 1 ) in vec3 normal;
layout( location = 2 ) in vec2 texCoords;

//
// Out
//
layout( location = 0 ) out struct VS_OUT {
  vec2 vTexCoords;
  vec3 vNormal;
  vec3 vPosition;
  vec3 vPositionWs;
  vec3 vPositionVs;
} vs_out;

void main() {
  vs_out.vTexCoords = texCoords;
  vs_out.vNormal = normalize( mat3( transpose( inverse( g_oUbo.g_mModel ) ) ) * normal );
  vs_out.vPosition = vec3( g_oUbo.g_mModel * vec4( position, 1.0 ));

  vec4 pos = g_oUbo.g_mModel * vec4( position, 1.0 );

  vs_out.vPositionVs = vec3( g_oUbo.g_mViewProj * pos );
  vs_out.vPositionWs = vec3( g_oUbo.g_mViewProj * pos );
  gl_Position = g_oUbo.g_mViewProj * pos;
}