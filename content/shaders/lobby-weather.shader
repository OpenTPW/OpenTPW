vertex {
    layout(location = 0) in vec3 position;
    layout(location = 1) in vec3 color;
    layout(location = 2) in vec2 texCoords;
    layout(location = 3) in int clipDepth;
    layout(location = 4) in uint opacity;
    layout(location = 0) out vec2 uv;
    layout(location = 1) out vec4 tint;
    void main() {
        float w = intBitsToFloat(clipDepth);
        gl_Position = vec4(position.xy, position.z * w, w);
        // RetailTexturePixels supplies bottom-first TGA rows; no additional V flip is needed.
        uv = texCoords;
        tint = vec4(color, float(opacity) / 255.0);
    }
}
fragment {
    layout(location = 0) in vec2 uv;
    layout(location = 1) in vec4 tint;
    layout(location = 0) out vec4 fragColor;
    layout(set = 0, binding = 0) uniform texture2D Color;
    layout(set = 0, binding = 1) uniform sampler s_Color;
    void main() {
        fragColor = texture(sampler2D(Color, s_Color), uv) * tint;
    }
}
