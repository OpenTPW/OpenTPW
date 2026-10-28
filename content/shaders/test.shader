vertex {
    layout(location = 0) in vec3 position;
    layout(location = 1) in vec3 normal;
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
        vec3 vNormal;
        vec3 vPosition;
        vec3 vWorldPosition;
    } vs_out;

    layout(location = 5) out flat int outTexIndex;
    layout(location = 6) out flat uint outMatFlags;

    void main() {
        vs_out.vTexCoords = texCoords;
        // Keep MD2 UVs intact on the CPU and pair them with top-first WCT uploads here.
        vs_out.vTexCoords.y = 1.0 - vs_out.vTexCoords.y;
        vs_out.vNormal = mat3(transpose(inverse(g_oUbo.g_mModel))) * normal;
        vs_out.vPosition = vec3(g_oUbo.g_mModel * vec4(position, 1.0));

        vec4 pos = g_oUbo.g_mModel * vec4(position, 1.0);
        vs_out.vWorldPosition = vec3(g_oUbo.g_mView * pos);
        gl_Position = g_oUbo.g_mProj * g_oUbo.g_mView * pos;

        outTexIndex = texIndex;
        outMatFlags = matFlags;
    }
}

fragment {
    layout(location = 0) in VS_OUT {
        vec2 vTexCoords;
        vec3 vNormal;
        vec3 vPosition;
        vec3 vWorldPosition;
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

    const uint FLAG_BASE = 1;
    const uint FLAG_TRANSPARENT = 2;
    const uint FLAG_UNK = 4;
    const uint FLAG_X_AXIS_ANIM = 8;
    const uint FLAG_Y_AXIS_ANIM = 16;
    const uint FLAG_GOURAUD_SHADED = 32;
        
    void main() 
    {
        vec2 finalTexCoords = vs_out.vTexCoords;

        /*if ((outMatFlags & FLAG_X_AXIS_ANIM) != 0) {
            finalTexCoords.x = finalTexCoords.x + g_oUbo.g_flTime * 0.1;
        }

        if ((outMatFlags & FLAG_Y_AXIS_ANIM) != 0) {
            finalTexCoords.y = finalTexCoords.y + g_oUbo.g_flTime * 0.1;
        }*/

        vec3 N = normalize(vs_out.vNormal);
        vec3 L = normalize(g_oUbo.g_vLightPos - vs_out.vPosition);
        
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
        vec3 vOutColor;

        // Apply Gouraud shading if the flag is set, otherwise use flat shading
        if ((outMatFlags & FLAG_GOURAUD_SHADED) != 0) {
            vOutColor = vTextureSample.xyz * vShading;
        } else {
            vOutColor = vTextureSample.xyz * vShading;
        }

        // Handle opaque alpha testing
        if (vTextureSample.a < 0.1) discard;

        fragColor = vec4(vOutColor, vTextureSample.a);

        // Calculate fog using view space depth
        float viewSpaceDepth = length(vs_out.vWorldPosition);
        float fogFactor = exp(viewSpaceDepth * 0.01) * 0.025;
        fogFactor = clamp( fogFactor, 0, 1 ) * g_oUbo.g_vFogColor.w;
        
        // Mix with fog
        fragColor.xyz = mix(fragColor.xyz, g_oUbo.g_vFogColor.xyz, fogFactor);
    }
}
