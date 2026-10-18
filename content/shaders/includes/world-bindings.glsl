// Binding layout shared by the model and terrain shaders.
layout(set = 0, binding = 0) uniform ObjectUniformBuffer {
    mat4 g_mModel;
    mat4 g_mViewProj;
    vec3 g_vLightPos;
    vec3 g_vLightColor;
    vec3 g_vCameraPos;
    float g_flTime;
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
