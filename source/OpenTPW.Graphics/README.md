# OpenTPW.Graphics

A small rendering core on SDL3 GPU, shared by OpenTPW and other projects built on the same engine data. It owns GPU objects and the recording and submission of a frame; scenes, entities, materials and shader selection stay in the game.

- **Device**: `GpuDevice.Create()` picks Direct3D 12 then Vulkan on Windows, Vulkan elsewhere, and loads DXIL or SPIR-V to match. It works without a window; `AttachWindow` adds presentation.
- **Frames**: draws and dynamic uploads are recorded and replayed at `Submit` in one command buffer (copy pass, render passes, presentation), so per-draw data can be written while drawing. Each of the frames in flight (2 by default) has its own dynamic buffer, reused only after its fence; per-draw constants use SDL's push uniforms, which are copied per call.
- **Pipelines** are cached by `GpuPipelineDescription`. The defaults match an OpenTPW material without flags; `Blend`, `DepthTest`, `DepthWrite`, `DepthCompare` and `Cull` cover the material flags.
- **Samplers**: `SamplerPreset` has every OpenTPW sampler type plus `PointMinLinearMag`.
- **Shaders**: `ShaderProgram.Load` reads the precompiled programs in `content/shaders/compiled` (see `content/shaders/README.md` for resource counts).

```csharp
using var device = GpuDevice.Create();
using var target = new RenderTarget( device, 1280, 720, sampleCount: SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_4 );
var ui = ShaderProgram.Load( device, shaderDirectory, "ui", default, new ShaderResources( Samplers: 1 ) );
var pipeline = device.GetPipeline( GpuPipelineDescription.For( target, ui, layout ) with { Cull = GpuCull.None } );

var frame = device.BeginFrame();
var pass = frame.BeginPass( target, GpuClear.ToColor( 0, 0, 0, 1 ) );
pass.Draw( pipeline, frame.Upload( vertices ), 6, [new TextureBinding( texture, device.GetSampler( SamplerPreset.Point ) )] );
pass.End();
frame.Present( target.Color ); // when a window is attached
frame.Submit();
```

SDL GPU uses Direct3D-style clip space (y up, depth 0 to 1). Tests in `OpenTPW.Tests/Graphics` render offscreen on a real device; set `OPENTPW_TEST_GPU_DRIVER=vulkan` to run them on the SPIR-V path.
