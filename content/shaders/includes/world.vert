// Shared model/terrain vertex stage. Uniforms come from world-bindings.glsl.
layout(location = 0) in vec3 position;
layout(location = 1) in vec3 normal;
layout(location = 2) in vec2 texCoords;
layout(location = 3) in int texIndex;
layout(location = 4) in uint matFlags;

layout(location = 0) out VS_OUT {
    vec2 vTexCoords;
    vec3 vNormal;
    vec3 vPosition;
    vec3 vWorldPosition;
    vec3 vPositionCS;
} vs_out;

layout(location = 5) out flat int outTexIndex;
layout(location = 6) out flat uint outMatFlags;

void main() {
    vs_out.vTexCoords = texCoords;
    vs_out.vTexCoords.y = 1.0 - vs_out.vTexCoords.y;
    vs_out.vNormal = mat3(transpose(inverse(g_oUbo.g_mModel))) * normal;
    vec4 pos = g_oUbo.g_mModel * vec4(position, 1.0);
    vs_out.vPosition = pos.xyz;
    vs_out.vWorldPosition = pos.xyz;
    vs_out.vPositionCS = (g_oUbo.g_mViewProj * pos).xyz;
    gl_Position = g_oUbo.g_mViewProj * pos;

    outTexIndex = texIndex;
    outMatFlags = matFlags;
}
