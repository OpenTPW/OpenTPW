vertex {
    layout(location = 0) in vec3 position;
    // LobbyModel stores the retail vertex colour (0x570ca0) in the normal slot.
    layout(location = 1) in vec3 color;
    layout(location = 2) in vec2 texCoords;
    layout(location = 3) in int texIndex;
    layout(location = 4) in uint matFlags;

    layout(set = 0, binding = 0) uniform ObjectUniformBuffer {
        mat4 g_mModel;
        mat4 g_mView;
        mat4 g_mProj;
        vec3 g_vLightPos;
        vec3 g_vLightColor;
        vec3 g_vCameraPos;
        float g_flTime;
        vec4 g_vFogColor;
    } g_oUbo;

    layout(location = 0) out VS_OUT {
        vec2 vTexCoords;
        vec3 vColor;
        float vDepth;
    } vs_out;

    layout(location = 5) out flat int outTexIndex;
    layout(location = 6) out flat uint outMatFlags;

    void main() {
        vs_out.vTexCoords = texCoords;
        // Keep MD2 UVs intact on the CPU and pair them with top-first WCT uploads here.
        vs_out.vTexCoords.y = 1.0 - vs_out.vTexCoords.y;
        vs_out.vColor = color;

        vec4 pos = g_oUbo.g_mModel * vec4(position, 1.0);
        vec4 view = g_oUbo.g_mView * pos;
        // Retail fog runs on camera-space depth (0x570e10 reads the transformed vertex z).
        vs_out.vDepth = abs(view.z);
        gl_Position = g_oUbo.g_mProj * view;

        outTexIndex = texIndex;
        outMatFlags = matFlags;
    }
}

fragment {
    layout(location = 0) in VS_OUT {
        vec2 vTexCoords;
        vec3 vColor;
        float vDepth;
    } vs_out;

    layout(location = 5) in flat int texIndex;
    layout(location = 6) in flat uint outMatFlags;

    layout(location = 0) out vec4 fragColor;

    layout(set = 0, binding = 0) uniform ObjectUniformBuffer {
        mat4 g_mModel;
        mat4 g_mView;
        mat4 g_mProj;
        vec3 g_vLightPos;
        vec3 g_vLightColor;
        vec3 g_vCameraPos;
        float g_flTime;
        vec4 g_vFogColor;
    } g_oUbo;

    layout( set = 1, binding = 0 ) uniform texture2D Color0;
    layout( set = 1, binding = 1 ) uniform texture2D Color1;
    layout( set = 1, binding = 2 ) uniform texture2D Color2;
    layout( set = 1, binding = 3 ) uniform texture2D Color3;
    layout( set = 1, binding = 4 ) uniform texture2D Color4;
    layout( set = 1, binding = 5 ) uniform texture2D Color5;
    layout( set = 1, binding = 6 ) uniform texture2D Color6;
    layout( set = 1, binding = 7 ) uniform texture2D Color7;
    layout( set = 1, binding = 8 ) uniform texture2D Color8;
    layout( set = 1, binding = 9 ) uniform texture2D Color9;
    layout( set = 1, binding = 10 ) uniform texture2D Color10;
    layout( set = 1, binding = 11 ) uniform texture2D Color11;
    layout( set = 1, binding = 12 ) uniform texture2D Color12;
    layout( set = 1, binding = 13 ) uniform texture2D Color13;
    layout( set = 1, binding = 14 ) uniform texture2D Color14;
    layout( set = 1, binding = 15 ) uniform texture2D Color15;
    layout( set = 1, binding = 16 ) uniform sampler s_Color;

    void main() 
    {
        vec2 finalTexCoords = vs_out.vTexCoords;

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

        // D3DTOP_MODULATE of texture and vertex diffuse (0x563c50 stage 0 colour op 4).
        vec3 vOutColor = vTextureSample.xyz * vs_out.vColor;

        // Handle opaque alpha testing
        if (vTextureSample.a < 0.1) discard;

        fragColor = vec4(vOutColor, vTextureSample.a);

        // Linear fog between 50 and 300 units (0x5d3c20 / 0x5d4770), enabled on island pages (0x5d4750).
        float fogFactor = clamp( (vs_out.vDepth - 50.0) / (300.0 - 50.0), 0.0, 1.0 ) * g_oUbo.g_vFogColor.w;
        fragColor.xyz = mix(fragColor.xyz, g_oUbo.g_vFogColor.xyz, fogFactor);
    }
}
