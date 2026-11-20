# Shader sources and precompiled SDL GPU programs

The SDL GPU renderer loads the precompiled DXIL or SPIR-V programs. Each `.hlsl` source has `VSMain` and `PSMain` entry points. The legacy `.shader` files and `includes/3d.vert` are retained as reference inputs to the binding verifier; the game no longer preprocesses or compiles them at run time.

## Rebuild

Use PowerShell and DXC 1.9.2609.5 (Shader Model 6.0, HLSL 2021):

```powershell
./tools/build-shaders.ps1 -Dxc <path-to-dxc>
./tools/build-shaders.ps1 -Dxc <path-to-dxc> -Check
python tools/verify-shaders.py --dxc <path-to-dxc>
```

DXC may also be selected through PATH. `-OutputDirectory <directory>` changes the output location. The script works independently of the current working directory, compiles all stages into a fresh temporary directory, and publishes the outputs only after every compilation succeeds. `-Check` compares all 44 binaries and `SHA256SUMS` without modifying them. A different DXC version can change the binary bytes.

Both formats use `-HV 2021 -Ges -WX -O3 -Zpc` with `vs_6_0` / `ps_6_0`. SPIR-V additionally uses `-spirv -fspv-target-env=vulkan1.0 -fvk-use-gl-layout`. Vulkan attributes are enabled only for SPIR-V. Relative source names and no debug information keep the outputs independent of checkout paths. The committed `compiled/` directory contains both formats and their SHA-256 hashes.

`verify-shaders.py` uses Python's standard library and `dxc -dumpbin` to check both binary formats. It checks every original GLSL resource against `binding-map.tsv`, verifies actual descriptor sets/register spaces and uniform offsets, checks active stage interfaces and combined image samplers, and verifies the binary file set and hashes. Use `--write-map` to regenerate the table after changing resource declarations. `--report-dir <directory>` saves DXIL dumps and SPIR-V reflection for review. This is a static check, not a rendered-image comparison.

## Bindings

`binding-map.tsv` has one row per original GLSL binding **per effective shader stage**, after prepending `common`. It covers all 63 bindings, including uniform block members and declarations optimized out of the programs.

The layout follows [SDL_CreateGPUShader](https://wiki.libsdl.org/SDL3/SDL_CreateGPUShader): vertex uniforms use `b0,space1` / SPIR-V set 1, fragment uniforms use `b0,space3` / set 3. Fragment texture/sampler pairs use `tN,sN,space2` / set 2 binding N. These shaders do not sample textures in the vertex stage. Non-system vertex semantics use `TEXCOORDn`; `LOCATION(n)` explicitly preserves Vulkan locations, including the particles' location 4 and flat model varyings at locations 5 and 6.

`TEXTURE_SLOT` uses DXC's [combined image sampler attribute](https://github.com/microsoft/DirectXShaderCompiler/wiki/Vulkan-combined-image-sampler-type) for SPIR-V. D3D12 retains separate texture/sampler registers; Vulkan gets one combined descriptor per pair. The original `lobby-model` and `test` shaders share one sampler among 16 textures. Bind that same sampler at all 16 SDL sampler slots; do not bind a separate seventeenth sampler. The table lists this one-to-many mapping explicitly.

| Program | Stage | Entry | num_samplers | num_uniform_buffers |
| --- | --- | --- | ---: | ---: |
| 3d, water, lobby-model, test | vertex | VSMain | 0 | 1 |
| 3d, water | fragment | PSMain | 1 | 1 |
| lobby-model, test | fragment | PSMain | 16 | 1 |
| unlit, lobby-sky | vertex | VSMain | 0 | 1 |
| unlit, lobby-sky | fragment | PSMain | 1 | 0 |
| blit, ui, ui-lit, lobby-particles, lobby-weather | vertex | VSMain | 0 | 0 |
| blit, ui, ui-lit, lobby-particles, lobby-weather | fragment | PSMain | 1 | 0 |

Storage texture/buffer counts are zero. The unlit fragment's declared object uniform is unused and disappears from both formats, so its runtime uniform count is zero. Unused vertex inputs and varyings can also be optimized away; buffer layouts must still use their original numeric locations. `blit` generates a full-screen triangle from `SV_VertexID` and needs no vertex buffer.

## Data and math conventions

Matrices remain column-major and matrix/vector products use `mul(matrix, vector)`, retaining the original GLSL interpretation of uploaded bytes. Do not add a CPU transpose for these shaders. Explicit padding preserves the original std140 member offsets: model/view/projection at 0/64/128, light position/color at 192/208, camera at 224, time at 236 and fog at 240. Full object and water uniforms occupy 256 bytes; legacy uniforms occupy 240 bytes. Water keeps its original `g_fTime` member name.

The port preserves the original UV flips, homogeneous clip position/depth, fog, alpha-test conditions, integer bit reinterpretation (`asfloat` for `intBitsToFloat`), and flat integer interpolation. No additional clip-space or texture-coordinate correction is applied here. The future renderer must supply the matching projection/viewport conventions when connecting these programs.

The test shader's normal transform computes the upper 3x3 of the full inverse-transpose using cofactors, since HLSL has no GLSL-style `inverse` intrinsic. It does not assume an affine model matrix. The original identical Gouraud flag branches and disabled UV animation remain unchanged in behavior. The 3d fragment's unused specular calculation has no output contribution and is omitted.
