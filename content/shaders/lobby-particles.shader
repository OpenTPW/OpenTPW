common {
  layout(location = 0) struct VS_OUT {
    vec2 texCoords;
    vec4 colour;
  };
}
vertex {
  layout(location = 0) in vec3 position;
  layout(location = 1) in vec3 normal;
  layout(location = 2) in vec2 texCoords;
  layout(location = 4) in uint alpha;
  layout(location = 0) out VS_OUT vs_out;
  void main() {
    vs_out.texCoords = texCoords;
    vs_out.colour = vec4(normal, float(alpha) / 255.0);
    gl_Position = vec4(position.xy, 0, 1);
  }
}
fragment {
  layout(set = 0, binding = 0) uniform texture2D Color;
  layout(set = 0, binding = 1) uniform sampler s_Color;
  layout(location = 0) in VS_OUT vs_out;
  layout(location = 0) out vec4 out_colour;
  void main() {
    out_colour = texture(sampler2D(Color, s_Color), vec2(vs_out.texCoords.x, 1 - vs_out.texCoords.y)) * vs_out.colour;
    if (out_colour.a == 0) discard;
  }
}
