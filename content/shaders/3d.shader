vertex "content/shaders/includes/3d.vert"

common {
  layout( set = 0, binding = 0 ) uniform ObjectUniformBuffer {
    mat4 g_mModel;
    mat4 g_mViewProj;

    vec3 g_vLightPos;
    vec3 g_vLightColor;
    vec3 g_vCameraPos;
  } g_oUbo;
}

fragment {
  //
  // In
  //
  layout( location = 0 ) in struct VS_OUT {
    vec2 vTexCoords;
    vec3 vNormal;
    vec3 vPosition;
    vec3 vPositionWs;
  } vs_out;

  //
  // Out
  //
  layout( location = 0 ) out vec4 fragColor;

  //
  // Uniforms
  //
  layout( set = 1, binding = 0 ) uniform texture2D Color;
  layout( set = 1, binding = 1 ) uniform sampler s_Color;

  vec3 lambert( vec3 vLightDir, vec3 vNormal ) 
  {
    return dot( normalize( vLightDir ), normalize( vNormal ) ) * g_oUbo.g_vLightColor;
  }

  vec3 ambient( vec3 vLightColor ) 
  {
    return vLightColor * 0.2;
  }

  vec3 specular( vec3 vLightDir, vec3 vNormal, vec3 vCameraDir, float fShininess ) 
  {
    vec3 vReflection = reflect( normalize( -vLightDir ), normalize( vNormal ) );
    return pow( max( dot( normalize( vCameraDir ), vReflection ), 0.0 ), fShininess ) * g_oUbo.g_vLightColor;
  }

  void main() 
  {
    vec3 vLightDir = normalize( g_oUbo.g_vLightPos - vs_out.vPosition );

    vec3 vLambert = lambert( vLightDir, vs_out.vNormal );
    vec3 vAmbient = ambient( g_oUbo.g_vLightColor );
    
    vec3 vCameraDir = normalize( g_oUbo.g_vCameraPos - vs_out.vPosition );
    vec3 vSpecular = specular( vLightDir, vs_out.vNormal, vCameraDir, 32.0 );

    vec4 vColor = texture( sampler2D( Color, s_Color ), vs_out.vTexCoords ) * 0.75;
    vColor.z *= 1.25;

    vec3 fogColor = vec3( 0.28, 0.88, 1.0 );
    float fogStart = 2.0;
    float fogEnd = 30.0;

    // float fogAmount = ( length( vs_out.vPositionWs ) - fogStart ) / ( fogEnd - fogStart );
    // fogAmount = clamp( fogAmount, 0.0, 1.0 );
    // smoother fog, better looking
    float fogAmount = ( 1.0 - exp( -length( vs_out.vPositionWs ) * 0.01 ) ) / ( 1.0 - exp( -fogEnd * 0.01 ) );
    fogAmount = clamp( fogAmount, 0.0, 1.0 );

    fragColor = vec4( vLambert + vAmbient, 1.0 ) * vColor;
    fragColor = mix( fragColor, vec4( fogColor, 1.0 ), fogAmount );

    if ( fragColor.w < 0.1 )
      discard;
  }
}