common "includes/world-bindings.glsl"
vertex "includes/world.vert"

fragment {
    layout(location = 0) in VS_OUT {
        vec2 vTexCoords;
        vec3 vNormal;
        vec3 vPosition;
        vec3 vWorldPosition;
        vec3 vPositionCS;
    } vs_out;

    layout(location = 5) in flat int texIndex;
    layout(location = 6) in flat uint outMatFlags;

    layout(location = 0) out vec4 fragColor;

    void main()
    {
        vec2 finalTexCoords = vs_out.vTexCoords;

        vec3 N = vs_out.vNormal / max(length(vs_out.vNormal), 0.00001);
        // StaticModelActor supplies the world-space direction toward the sun.
        vec3 L = normalize(g_oUbo.g_vLightPos);

        vec3 vDiffuse = max(dot(N, L), 0.0) * g_oUbo.g_vLightColor;
        vec3 vAmbient = vec3(0.4);

        vec4 vTextureSample = vec4(1, 0, 1, 1);

        if ( texIndex == 0 ) vTextureSample = texture( sampler2D( Color0, s_Color ), finalTexCoords);
        if ( texIndex == 1 ) vTextureSample = texture( sampler2D( Color1, s_Color ), finalTexCoords);
        if ( texIndex == 2 ) vTextureSample = texture( sampler2D( Color2, s_Color ), finalTexCoords);
        if ( texIndex == 3 ) vTextureSample = texture( sampler2D( Color3, s_Color ), finalTexCoords);
        if ( texIndex == 4 ) vTextureSample = texture( sampler2D( Color4, s_Color ), finalTexCoords);
        if ( texIndex == 5 ) vTextureSample = texture( sampler2D( Color5, s_Color ), finalTexCoords);
        if ( texIndex == 6 ) vTextureSample = texture( sampler2D( Color6, s_Color ), finalTexCoords);
        if ( texIndex == 7 ) vTextureSample = texture( sampler2D( Color7, s_Color ), finalTexCoords);
        if ( texIndex == 8 ) vTextureSample = texture( sampler2D( Color8, s_Color ), finalTexCoords);
        if ( texIndex == 9 ) vTextureSample = texture( sampler2D( Color9, s_Color ), finalTexCoords);
        if ( texIndex == 10 ) vTextureSample = texture( sampler2D( Color10, s_Color ), finalTexCoords);
        if ( texIndex == 11 ) vTextureSample = texture( sampler2D( Color11, s_Color ), finalTexCoords);
        if ( texIndex == 12 ) vTextureSample = texture( sampler2D( Color12, s_Color ), finalTexCoords);
        if ( texIndex == 13 ) vTextureSample = texture( sampler2D( Color13, s_Color ), finalTexCoords);
        if ( texIndex == 14 ) vTextureSample = texture( sampler2D( Color14, s_Color ), finalTexCoords);
        if ( texIndex == 15 ) vTextureSample = texture( sampler2D( Color15, s_Color ), finalTexCoords);

        vec3 vShading = vDiffuse + vAmbient;
        // World materials use directional lighting. Raw MD2 flags are kept
        // for future decoding, not treated as a guessed "unshaded" mask.
        vec3 vOutColor = vTextureSample.xyz * vShading;

        fragColor = vec4(vOutColor, vTextureSample.a);

        // Calculate fog using view space depth
        float viewSpaceDepth = length(vs_out.vPositionCS);
        float fogFactor = exp(viewSpaceDepth * 0.01) * 0.025;
        fogFactor = clamp( fogFactor, 0, 1 );

        if ( fragColor.a < 0.1 )
            discard;

        // Mix with fog
        fragColor.xyz = mix(fragColor.xyz, vec3( 0.301, 0.84, 1 ), fogFactor);
    }
}
