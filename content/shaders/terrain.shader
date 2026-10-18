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

    // ParkTerrainActor binds: control map at 0, six colour tiles at 1..6,
    // seven masks at 7..13, and the five-row blend lookup at 14.
    const float TileOverlapPixels = 64.0;
    // Original cells span ten world units and advance 64 texture pixels.
    const float PixelsPerWorldUnit = 6.4;
    const float BlendLookupRows = 5.0;
    const float DirtBlendRow = 0.0;
    const float LawnBlendRow = 1.0;
    const float GrassCoverageRow = 2.0;
    const float LushBlendRow = 3.0;
    const float FlowerCoverageRow = 4.0;

    // Reconstructed lighting and fog, matching the world shader's palette.
    const float AmbientLight = 0.4;
    const float DirectionalLight = 0.6;
    const float FogGrowth = 0.006;
    const float FogStrength = 0.0125;
    const vec3 FogColour = vec3(0.301, 0.84, 1.0);

    vec2 tileUV(vec2 world, ivec2 size) {
        // Repeat the detailed tiles with the original tile-width overlap.
        vec2 pixels = mod(world * PixelsPerWorldUnit, vec2(size) - TileOverlapPixels);
        return (pixels + 0.5) / vec2(size);
    }

    vec4 sampleTile(texture2D tile, vec2 world) {
        ivec2 size = textureSize(sampler2D(tile, s_Color), 0);
        return texture(sampler2D(tile, s_Color), tileUV(world, size));
    }

    float blendRamp(float value, float row) {
        // Half a texel selects the centre of a lookup row.
        vec2 lookupUV = vec2(clamp(value, 0.0, 1.0), (row + 0.5) / BlendLookupRows);
        return texture(sampler2D(Color14, s_Color), lookupUV).r;
    }

    void main() {
        vec4 control = texture(sampler2D(Color0, s_Color), vs_out.vTexCoords);
        if (control.a < 0.5) {
            discard; // Scenery meshes occupy the holes in the ground.
        }

        vec2 world = vs_out.vWorldPosition.xy;
        vec3 dirtBase = sampleTile(Color1, world).rgb;
        vec3 dirtDetail = sampleTile(Color2, world).rgb;
        vec3 lawnBase = sampleTile(Color3, world).rgb;
        vec3 lawnDetail = sampleTile(Color4, world).rgb;
        vec3 lushBase = sampleTile(Color5, world).rgb;
        vec3 lushDetail = sampleTile(Color6, world).rgb;

        float dirtMask = sampleTile(Color7, world).r;
        float lawnMask = sampleTile(Color8, world).r;
        float lushMask = sampleTile(Color9, world).r;
        float grassMaskA = sampleTile(Color10, world).r;
        float grassMaskB = sampleTile(Color11, world).r;
        float flowerMaskA = sampleTile(Color12, world).r;
        float flowerMaskB = sampleTile(Color13, world).r;

        // Red blends each material pair; green covers dirt with lawn;
        // blue adds the lush/flower layer over that result.
        float grass = max(blendRamp(control.g * grassMaskA, GrassCoverageRow),
                          blendRamp(control.g * grassMaskB, GrassCoverageRow));
        float flowers = max(blendRamp(control.b * flowerMaskA, FlowerCoverageRow),
                            blendRamp(control.b * flowerMaskB, FlowerCoverageRow));
        vec3 dirt = mix(dirtBase, dirtDetail, blendRamp(control.r * dirtMask, DirtBlendRow));
        vec3 lawn = mix(lawnBase, lawnDetail, blendRamp(control.r * lawnMask, LawnBlendRow));
        vec3 lush = mix(lushBase, lushDetail, blendRamp(control.r * lushMask, LushBlendRow));
        vec3 colour = mix(mix(dirt, lawn, grass), lush, flowers);

        vec3 normal = normalize(vs_out.vNormal);
        float light = AmbientLight + max(dot(normal, normalize(g_oUbo.g_vLightPos)), 0.0) * DirectionalLight;
        float depth = length(vs_out.vPositionCS);
        float fog = clamp(exp(depth * FogGrowth) * FogStrength, 0.0, 1.0);
        fragColor = vec4(mix(colour * light, FogColour, fog), 1.0);
    }
}
