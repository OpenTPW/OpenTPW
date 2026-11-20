vertex {
    layout(location = 0) in vec3 position;
    layout(location = 1) in vec3 color;
    layout(location = 2) in vec2 texCoords;
    layout(location = 3) in int texIndex;
    layout(location = 4) in uint opacity;
    layout(set = 0, binding = 0) uniform ObjectUniformBuffer {
        mat4 g_mModel;
        mat4 g_mView;
        mat4 g_mProj;
        vec3 g_vLightPos;
        float _padding0;
        vec3 g_vLightColor;
        float _padding1;
        vec3 g_vCameraPos;
        float g_flTime;
        vec4 g_vFogColor;
    } g_oUbo;
    layout(location = 0) out vec2 uv;
    layout(location = 1) out vec4 tint;
    void main() {
        uv = texCoords;
        tint = vec4(color, float(opacity) / 255.0);
        gl_Position = g_oUbo.g_mProj * g_oUbo.g_mView * g_oUbo.g_mModel * vec4(position, 1.0);
    }
}
fragment {
    layout(location = 0) in vec2 uv;
    layout(location = 1) in vec4 tint;
    layout(location = 0) out vec4 fragColor;
    layout(set = 1, binding = 0) uniform texture2D Color;
    layout(set = 1, binding = 1) uniform sampler s_Color;
    void main() {
        fragColor = texture(sampler2D(Color, s_Color), uv) * tint;
    }
}
