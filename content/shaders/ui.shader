common {
  layout( location = 0 ) struct VS_OUT {
    vec2 texCoords;
  };
}

vertex {
  layout( location = 0 ) in vec3 position;
  layout( location = 1 ) in vec3 normal;
  layout( location = 2 ) in vec2 texCoords;

  layout( location = 0 ) out VS_OUT vs_out;

  void main() {
    vs_out.texCoords = texCoords;
    gl_Position = vec4(position, 1.0);
    gl_Position.z = 0; // We could probably do this better in a dedicated pipeline
  }
}

fragment {
  layout( set = 0, binding = 0 ) uniform texture2D Color;
  layout( set = 0, binding = 1 ) uniform sampler s_Color;

  layout( location = 0 ) in VS_OUT vs_out;
  layout( location = 0 ) out vec4 out_color;

  void main() {
    out_color = texture( sampler2D( Color, s_Color ), vec2( vs_out.texCoords.x, 1 - vs_out.texCoords.y ) );

    // Veldrid has no support for native alpha-testing, so we have to pull this shit
    // If it causes too many performance issues I guess we'll have to sort on CPU
    if ( out_color.a == 0 )
        discard;
  }
}

