vertex {
    layout(location = 0) in vec3 position;
    layout(location = 2) in vec2 texCoords;
    layout(location = 0) out vec2 uv;
    void main() { uv = texCoords; gl_Position = vec4(position.xy, 0, 1); }
}
fragment {
    layout(set = 0, binding = 0) uniform texture2D Color;
    layout(set = 0, binding = 1) uniform sampler s_Color;
    layout(set = 0, binding = 2) uniform ParticleColor { vec4 tint; } particle;
    layout(location = 0) in vec2 uv;
    layout(location = 0) out vec4 out_color;
    void main() {
        vec4 star = texture(sampler2D(Color, s_Color), uv);
        out_color = vec4(star.rgb * particle.tint.rgb, star.a * particle.tint.a);
    }
}
