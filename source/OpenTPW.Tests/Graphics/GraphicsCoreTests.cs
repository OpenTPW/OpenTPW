using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.Graphics;
using SDL;
// The library takes plain System.Numerics data; OpenTPW has its own types named Vector2, Vector3 and Vector4.
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;
using NVector4 = System.Numerics.Vector4;

namespace OpenTPW.Tests.Graphics;

/// <summary>
/// Offscreen checks of the shared SDL GPU core on a real device, drawn with the repository's precompiled
/// <c>ui</c> and <c>unlit</c> shaders. A missing GPU backend fails these tests rather than skipping them.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed unsafe class GraphicsCoreTests
{
	private static readonly string ShaderDirectory = Path.Combine( AppContext.BaseDirectory, "content", "shaders", "compiled" );
	private const SDL_GPUTextureFormat Rgba = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM;
	// Null only when initialisation failed; cleanup then skips what was not created.
	private GpuDevice device = null!;
	private ShaderProgram ui = null!, unlit = null!;

	/// <summary>The vertex layout of the repository's MeshInput (content/shaders/includes/sdl-gpu.hlsli).</summary>
	[StructLayout( LayoutKind.Sequential )]
	private readonly record struct MeshVertex( NVector3 Position, NVector3 Normal, NVector2 TexCoords )
	{
		public static readonly GpuVertexLayout Layout = new( 32,
			new GpuVertexAttribute( 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3, 0 ),
			new GpuVertexAttribute( 1, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3, 12 ),
			new GpuVertexAttribute( 2, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 24 ) );
	}

	/// <summary>LegacyObjectUniforms: three column-major matrices and three padded vectors, 240 bytes.</summary>
	[StructLayout( LayoutKind.Sequential )]
	private struct LegacyObjectUniforms
	{
		public Matrix4x4 Model, View, Projection;
		public NVector4 LightPosition, LightColor, CameraPosition;
	}

	[TestInitialize]
	public void CreateDevice()
	{
		// OPENTPW_TEST_GPU_DRIVER=vulkan runs the same checks on the SPIR-V path; the default is the platform order.
		var driver = Environment.GetEnvironmentVariable( "OPENTPW_TEST_GPU_DRIVER" );
		device = GpuDevice.Create( new GpuDeviceOptions { Drivers = string.IsNullOrEmpty( driver ) ? null : [driver] } );
		ui = ShaderProgram.Load( device, ShaderDirectory, "ui", default, new ShaderResources( Samplers: 1 ) );
		unlit = ShaderProgram.Load( device, ShaderDirectory, "unlit", new ShaderResources( UniformBuffers: 1 ), new ShaderResources( Samplers: 1 ) );
	}

	[TestCleanup]
	public void DestroyDevice()
	{
		ui?.Dispose();
		unlit?.Dispose();
		device?.Dispose();
	}

	/// <summary>A rectangle in clip space; the ui shader samples (u, 1 - v), so v = 1 at the top maps texel row 0 to the top.</summary>
	private static MeshVertex[] Quad( float left, float top, float right, float bottom, float z = 0, NVector2? uv = null )
	{
		MeshVertex V( float x, float y, float u, float v ) => new( new NVector3( x, y, z ), NVector3.UnitZ, uv ?? new NVector2( u, v ) );
		return [V( left, top, 0, 1 ), V( right, top, 1, 1 ), V( right, bottom, 1, 0 ), V( left, top, 0, 1 ), V( right, bottom, 1, 0 ), V( left, bottom, 0, 0 )];
	}

	private static readonly MeshVertex[] FullScreen = Quad( -1, 1, 1, -1 );

	private GpuTexture Texture( uint width, uint height, params uint[] rgba )
	{
		var texture = GpuTexture.Create2D( device, width, height );
		texture.Upload( MemoryMarshal.AsBytes( rgba.Select( c => (uint)((c >> 24) | ((c >> 8) & 0xff00) | ((c << 8) & 0xff0000) | (c << 24)) ).ToArray().AsSpan() ) );
		return texture;
	}

	private GpuPipeline Pipeline( RenderTarget target, ShaderProgram program, GpuBlend blend = GpuBlend.None, bool depthTest = false )
		=> device.GetPipeline( GpuPipelineDescription.For( target, program, MeshVertex.Layout ) with
		{
			Blend = blend, Cull = GpuCull.None, DepthTest = depthTest, DepthWrite = depthTest
		} );

	private static (byte R, byte G, byte B, byte A) Pixel( byte[] pixels, RenderTarget target, uint x, uint y )
	{
		int i = (int)((y * target.Width + x) * 4);
		return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
	}

	private static void AssertPixel( (byte R, byte G, byte B, byte A) actual, double r, double g, double b, double a, string what, int tolerance = 2 )
	{
		int[] expected = [(int)Math.Round( r * 255 ), (int)Math.Round( g * 255 ), (int)Math.Round( b * 255 ), (int)Math.Round( a * 255 )];
		int[] got = [actual.R, actual.G, actual.B, actual.A];
		for ( int i = 0; i < 4; i++ )
			if ( Math.Abs( expected[i] - got[i] ) > tolerance )
				Assert.Fail( $"{what}: expected {string.Join( ",", expected )}, got {string.Join( ",", got )}" );
	}

	/// <summary>Records one pass, submits and reads the target back.</summary>
	private byte[] Render( RenderTarget target, GpuClear clear, Action<GpuFrame, GpuRenderPass> draw )
	{
		var frame = device.BeginFrame();
		var pass = frame.BeginPass( target, clear );
		draw( frame, pass );
		pass.End();
		frame.Submit();
		return target.Color.Download();
	}

	[TestMethod]
	public void DeviceSelectsABackendAndShaderFormat()
	{
		Assert.IsTrue( device.Driver is "direct3d12" or "vulkan", device.Driver );
		var expected = device.Driver == "direct3d12" ? SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL : SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV;
		Assert.AreEqual( expected, device.ShaderFormat );
		Assert.AreNotEqual( SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_INVALID, device.DepthFormat );
	}

	[TestMethod]
	public void ClearFillsEveryPixel()
	{
		using var target = new RenderTarget( device, 17, 9, Rgba, depth: false );
		var pixels = Render( target, GpuClear.ToColor( 0.25f, 0.5f, 0.75f, 1 ), ( _, _ ) => { } );
		for ( uint y = 0; y < target.Height; y++ )
			for ( uint x = 0; x < target.Width; x++ )
				AssertPixel( Pixel( pixels, target, x, y ), 0.25, 0.5, 0.75, 1, $"pixel {x},{y}" );
	}

	[TestMethod]
	public void TexturesSampleWithPointAndLinearFilters()
	{
		// Row 0: red, green; row 1: blue, white.
		using var texture = Texture( 2, 2, 0xff0000ff, 0x00ff00ff, 0x0000ffff, 0xffffffff );
		using var target = new RenderTarget( device, 63, 63, Rgba, depth: false );
		var pipeline = Pipeline( target, ui );
		byte[] Draw( SamplerPreset preset ) => Render( target, GpuClear.ToColor( 0, 0, 0, 1 ), ( frame, pass ) =>
			pass.Draw( pipeline, frame.Upload( FullScreen ), 6, [new TextureBinding( texture, device.GetSampler( preset ) )] ) );

		var point = Draw( SamplerPreset.Point );
		AssertPixel( Pixel( point, target, 5, 5 ), 1, 0, 0, 1, "top left" );
		AssertPixel( Pixel( point, target, 57, 5 ), 0, 1, 0, 1, "top right" );
		AssertPixel( Pixel( point, target, 5, 57 ), 0, 0, 1, 1, "bottom left" );
		AssertPixel( Pixel( point, target, 57, 57 ), 1, 1, 1, 1, "bottom right" );
		// Pixel 31's centre is exactly the texture centre, where bilinear filtering averages all four texels.
		AssertPixel( Pixel( Draw( SamplerPreset.Linear ), target, 31, 31 ), 0.5, 0.5, 0.5, 1, "linear centre" );
	}

	[TestMethod]
	public void BlendModesMatchTheMaterialFlagFormulas()
	{
		using var texture = Texture( 1, 1, 0xff000080 );
		using var target = new RenderTarget( device, 4, 4, Rgba, depth: false );
		double a = 0x80 / 255.0;
		// Source (1, 0, 0, a) over destination (0, 0, 1, 1).
		var cases = new (GpuBlend Blend, double R, double G, double B, double A)[]
		{
			(GpuBlend.None, 1, 0, 0, a),
			(GpuBlend.Alpha, a, 0, 1 - a, a * a + (1 - a)),
			(GpuBlend.Additive, a, 0, 1, 1),
			(GpuBlend.InvertDestination, 1, 0, 0, 0),
		};
		foreach ( var (blend, r, g, b, alpha) in cases )
		{
			var pipeline = Pipeline( target, ui, blend );
			var pixels = Render( target, GpuClear.ToColor( 0, 0, 1, 1 ), ( frame, pass ) =>
				pass.Draw( pipeline, frame.Upload( FullScreen ), 6, [new TextureBinding( texture, device.GetSampler( SamplerPreset.Point ) )] ) );
			AssertPixel( Pixel( pixels, target, 1, 1 ), r, g, b, alpha, blend.ToString() );
		}
	}

	private static ReadOnlySpan<byte> Translation( float x, float y, float z )
	{
		var uniforms = new LegacyObjectUniforms
		{
			Model = Matrix4x4.CreateTranslation( x, y, z ), View = Matrix4x4.Identity, Projection = Matrix4x4.Identity
		};
		return GpuUniforms.Bytes( in uniforms ).ToArray();
	}

	[TestMethod]
	public void DepthTestKeepsTheNearerSurface()
	{
		using var red = Texture( 1, 1, 0xff0000ff );
		using var green = Texture( 1, 1, 0x00ff00ff );
		using var target = new RenderTarget( device, 8, 8, Rgba );
		var point = device.GetSampler( SamplerPreset.Point );
		foreach ( bool depthTest in new[] { true, false } )
		{
			var pipeline = Pipeline( target, unlit, depthTest: depthTest );
			var pixels = Render( target, new GpuClear( new SDL_FColor(), 1 ), ( frame, pass ) =>
			{
				var quad = frame.Upload( FullScreen );
				// SDL GPU depth runs from 0 (near) to 1; red is nearer and drawn first.
				pass.Draw( pipeline, quad, 6, [new TextureBinding( red, point )], Translation( 0, 0, 0.3f ) );
				pass.Draw( pipeline, quad, 6, [new TextureBinding( green, point )], Translation( 0, 0, 0.6f ) );
			} );
			if ( depthTest ) AssertPixel( Pixel( pixels, target, 4, 4 ), 1, 0, 0, 1, "depth test on" );
			else AssertPixel( Pixel( pixels, target, 4, 4 ), 0, 1, 0, 1, "depth test off" );
		}
	}

	[TestMethod]
	public void MultisampledTargetsResolveIntoColor()
	{
		var samples = device.MaxSampleCount( Rgba );
		Assert.AreNotEqual( SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1, samples, "The device offers no multisampling for RGBA8." );
		using var texture = Texture( 1, 1, 0x3366ccff );
		using var target = new RenderTarget( device, 16, 16, Rgba, samples );
		var pipeline = Pipeline( target, ui );
		var pixels = Render( target, GpuClear.ToColor( 0, 0, 0, 1 ), ( frame, pass ) =>
			pass.Draw( pipeline, frame.Upload( FullScreen ), 6, [new TextureBinding( texture, device.GetSampler( SamplerPreset.Point ) )] ) );
		for ( uint y = 0; y < 16; y++ )
			for ( uint x = 0; x < 16; x++ )
				AssertPixel( Pixel( pixels, target, x, y ), 0x33 / 255.0, 0x66 / 255.0, 0xcc / 255.0, 1, $"resolved {x},{y}" );
	}

	/// <summary>
	/// 120 frames are submitted without waiting, each drawing four quads whose colour comes from that frame's dynamic
	/// vertices and whose position comes from that draw's uniforms. Reused frame slots, overlapping ranges within a
	/// frame or shared uniform storage would show up as a wrong colour or a missing quadrant.
	/// </summary>
	[TestMethod]
	public void EveryFrameDrawsItsOwnDynamicDataAndUniforms()
	{
		const int frames = 120, draws = 4, entries = frames * draws;
		static uint Colour( int i ) => (uint)(((i & 0xff) << 24) | ((i >> 8) * 0x40 + 0x20) << 16 | 0xc8 << 8 | 0xff);
		using var palette = Texture( entries, 1, Enumerable.Range( 0, entries ).Select( Colour ).ToArray() );
		var targets = Enumerable.Range( 0, frames ).Select( _ => new RenderTarget( device, 8, 8, Rgba, depth: false ) ).ToArray();
		try
		{
			var pipeline = Pipeline( targets[0], unlit );
			var point = device.GetSampler( SamplerPreset.Point );
			var offsets = new (float X, float Y)[] { (-0.5f, 0.5f), (0.5f, 0.5f), (-0.5f, -0.5f), (0.5f, -0.5f) };
			for ( int f = 0; f < frames; f++ )
			{
				var frame = device.BeginFrame();
				var pass = frame.BeginPass( targets[f], GpuClear.ToColor( 0, 0, 0, 0 ) );
				for ( int d = 0; d < draws; d++ )
				{
					var uv = new NVector2( (f * draws + d + 0.5f) / entries, 0.5f );
					var quad = frame.Upload( Quad( -0.5f, 0.5f, 0.5f, -0.5f, 0, uv ) );
					pass.Draw( pipeline, quad, 6, [new TextureBinding( palette, point )], Translation( offsets[d].X, offsets[d].Y, 0 ) );
				}
				pass.End();
				frame.Submit();
			}
			for ( int f = 0; f < frames; f++ )
			{
				var pixels = targets[f].Color.Download();
				var corners = new (uint X, uint Y)[] { (2, 2), (6, 2), (2, 6), (6, 6) };
				for ( int d = 0; d < draws; d++ )
				{
					uint c = Colour( f * draws + d );
					AssertPixel( Pixel( pixels, targets[f], corners[d].X, corners[d].Y ),
						(c >> 24) / 255.0, ((c >> 16) & 0xff) / 255.0, ((c >> 8) & 0xff) / 255.0, 1, $"frame {f} draw {d}", tolerance: 0 );
				}
			}
		}
		finally { foreach ( var target in targets ) target.Dispose(); }
	}

	[TestMethod]
	public void PipelinesAreCachedByDescription()
	{
		using var target = new RenderTarget( device, 4, 4, Rgba );
		var alpha = device.GetPipeline( GpuPipelineDescription.For( target, ui, MeshVertex.Layout ) );
		var again = device.GetPipeline( GpuPipelineDescription.For( target, ui, new GpuVertexLayout( 32, [.. MeshVertex.Layout.Attributes] ) ) );
		var additive = device.GetPipeline( GpuPipelineDescription.For( target, ui, MeshVertex.Layout ) with { Blend = GpuBlend.Additive } );
		Assert.AreSame( alpha, again );
		Assert.AreNotSame( alpha, additive );
		Assert.AreSame( device.GetSampler( SamplerPreset.PointMinLinearMag ), device.GetSampler( SamplerPreset.PointMinLinearMag ) );
		foreach ( var preset in Enum.GetValues<SamplerPreset>() ) Assert.IsNotNull( device.GetSampler( preset ) );
	}

	[TestMethod]
	public void ObjectsDisposedDuringRecordingSurviveUntilSubmission()
	{
		var texture = Texture( 1, 1, 0x00ff00ff );
		using var target = new RenderTarget( device, 4, 4, Rgba, depth: false );
		var pipeline = Pipeline( target, ui );
		var pixels = Render( target, GpuClear.ToColor( 0, 0, 0, 1 ), ( frame, pass ) =>
		{
			var vertices = GpuBuffer.Create<MeshVertex>( device, GpuBufferUsage.Vertex, FullScreen );
			pass.Draw( pipeline, vertices, 6, [new TextureBinding( texture, device.GetSampler( SamplerPreset.Point ) )] );
			// Both releases are deferred: the recorded draw still refers to them.
			vertices.Dispose();
			texture.Dispose();
		} );
		AssertPixel( Pixel( pixels, target, 2, 2 ), 0, 1, 0, 1, "after release during recording" );
	}

	[TestMethod]
	public void RecordingRulesAreEnforced()
	{
		using var target = new RenderTarget( device, 4, 4, Rgba, depth: false );
		using var other = new RenderTarget( device, 4, 4, Rgba );
		var frame = device.BeginFrame();
		Assert.ThrowsExactly<InvalidOperationException>( () => device.BeginFrame() );
		var stale = frame.Upload( FullScreen );
		var pass = frame.BeginPass( target, GpuClear.Load );
		// A pipeline for a depth target cannot draw into a colour-only target.
		var mismatched = device.GetPipeline( GpuPipelineDescription.For( other, ui, MeshVertex.Layout ) );
		Assert.ThrowsExactly<ArgumentException>( () => pass.Draw( mismatched, stale, 6 ) );
		pass.End();
		frame.Submit();
		var next = device.BeginFrame();
		var nextPass = next.BeginPass( target, GpuClear.Load );
		Assert.ThrowsExactly<ArgumentException>( () => nextPass.Draw( Pipeline( target, ui ), stale, 6 ) );
		nextPass.End();
		next.Submit();
	}

	[TestMethod]
	public void FramesPresentToAnAttachedWindow()
	{
		Assert.IsTrue( SDL3.SDL_InitSubSystem( SDL_InitFlags.SDL_INIT_VIDEO ), SDL3.SDL_GetError() );
		var window = SDL3.SDL_CreateWindow( "graphics core", 64, 48, SDL_WindowFlags.SDL_WINDOW_HIDDEN );
		Assert.IsTrue( window != null, SDL3.SDL_GetError() );
		try
		{
			device.AttachWindow( window );
			Assert.AreNotEqual( SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_INVALID, device.SwapchainFormat );
			using var target = new RenderTarget( device, 64, 48, Rgba, depth: false );
			for ( int i = 0; i < 3; i++ )
			{
				var frame = device.BeginFrame();
				frame.BeginPass( target, GpuClear.ToColor( 0, 0.5f, 0, 1 ) ).End();
				frame.Present( target.Color );
				frame.Submit();
			}
			AssertPixel( Pixel( target.Color.Download(), target, 1, 1 ), 0, 0.5, 0, 1, "presented frame" );
			device.DetachWindow();
		}
		finally
		{
			SDL3.SDL_DestroyWindow( window );
			SDL3.SDL_QuitSubSystem( SDL_InitFlags.SDL_INIT_VIDEO );
		}
	}
}
