common {
  layout(location = 0) struct VS_OUT {
    vec2 texCoords;
  };
}

vertex {
  layout(location = 0) in vec3 position;
  layout(location = 1) in vec3 normal;
  layout(location = 2) in vec2 texCoords;
  layout(location = 0) out VS_OUT vs_out;

  void main() {
    vs_out.texCoords = texCoords;
    gl_Position = vec4(position.xy, 0.0, 1.0);
  }
}

fragment {
  layout(set = 0, binding = 0) uniform texture2D Color;
  layout(set = 0, binding = 1) uniform sampler s_Color;
  layout(set = 0, binding = 2) uniform TextParameters {
    vec4 textColor;
    vec4 atlasInfo; // distance range in atlas pixels, width, height, outline width in screen pixels
  };
  layout(location = 0) in VS_OUT vs_out;
  layout(location = 0) out vec4 out_color;

  float median(vec3 value) {
    return max(min(value.r, value.g), min(max(value.r, value.g), value.b));
  }

  void main() {
    vec3 distances = texture(sampler2D(Color, s_Color), vs_out.texCoords).rgb;
    vec2 unitRange = vec2(atlasInfo.x) / atlasInfo.yz;
    vec2 screenTexSize = vec2(1.0) / max(fwidth(vs_out.texCoords), vec2(1e-6));
    float screenRange = max(0.5 * dot(unitRange, screenTexSize), 1.0);
    float coverage = clamp(screenRange * (median(distances) - 0.5) + 0.5, 0.0, 1.0);
    if (atlasInfo.w > 0.0) {
      float interior = clamp(screenRange * (median(distances) - 0.5) - atlasInfo.w + 0.5, 0.0, 1.0);
      coverage -= interior;
    }
    out_color = vec4(textColor.rgb, textColor.a * coverage);
  }
}
