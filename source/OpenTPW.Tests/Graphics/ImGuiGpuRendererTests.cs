using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.Graphics;
using OpenTPW.ModKit;
using SDL;

namespace OpenTPW.Tests.Graphics;

/// <summary>
/// Offscreen checks of the ModKit ImGui SDL GPU backend and render-pass scissor clipping on a real device.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed unsafe class ImGuiGpuRendererTests
{
	private static readonly string ShaderDirectory = Path.Combine( AppContext.BaseDirectory, "content", "shaders", "compiled" );
	private const SDL_GPUTextureFormat Rgba = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM;
	private GpuDevice device = null!;

	[TestInitialize]
	public void CreateDevice()
	{
		var driver = Environment.GetEnvironmentVariable( "OPENTPW_TEST_GPU_DRIVER" );
		device = GpuDevice.Create( new GpuDeviceOptions { Drivers = string.IsNullOrEmpty( driver ) ? null : [driver] } );
	}

	[TestCleanup]
	public void DestroyDevice() => device?.Dispose();

	[TestMethod]
	public void RendersImGuiWindowAndScissorClipsOutsideRegion()
	{
		using var target = new RenderTarget( device, 64, 64, Rgba );
		using var imgui = new ImGuiGpuRenderer( device, ShaderDirectory );

		imgui.NewFrame( new EditorInput(), new Vector2( 64, 64 ), 1f / 60f );
		var drawList = ImGui.GetBackgroundDrawList();
		drawList.PushClipRect( new Vector2( 16, 16 ), new Vector2( 48, 48 ) );
		drawList.AddRectFilled( Vector2.Zero, new Vector2( 64, 64 ), 0xFF00FF00 ); // ABGR: solid green
		drawList.PopClipRect();

		var frame = device.BeginFrame();
		var pass = frame.BeginPass( target, GpuClear.ToColor( 0, 0, 0, 1 ) );
		imgui.Render( frame, pass );
		pass.End();
		frame.Submit();
		device.WaitForIdle();

		var pixels = target.Color.Download();
		AssertPixel( pixels, 64, 8, 8, 0, 0, 0, 255, "outside scissor" );
		AssertPixel( pixels, 64, 32, 32, 0, 255, 0, 255, "inside scissor" );
	}

	[TestMethod]
	public void RendersCustomTextureAndDefersDestroyUntilNextFrame()
	{
		using var target = new RenderTarget( device, 32, 32, Rgba );
		using var imgui = new ImGuiGpuRenderer( device, ShaderDirectory );
		int initialCount = imgui.TextureCount;

		// 1x1 red pixel (RGBA)
		var textureId = imgui.CreateTexture( new byte[] { 255, 0, 0, 255 }, 1, 1 );
		Assert.AreEqual( initialCount + 1, imgui.TextureCount );

		imgui.NewFrame( new EditorInput(), new Vector2( 32, 32 ), 1f / 60f );
		ImGui.GetBackgroundDrawList().AddImage( textureId, Vector2.Zero, new Vector2( 32, 32 ) );

		// Destroying mid-frame must keep the texture alive for this frame's Render call.
		imgui.DestroyTexture( textureId );
		Assert.AreEqual( initialCount + 1, imgui.TextureCount );

		var frame = device.BeginFrame();
		var pass = frame.BeginPass( target, GpuClear.ToColor( 0, 0, 0, 1 ) );
		imgui.Render( frame, pass );
		pass.End();
		frame.Submit();
		device.WaitForIdle();

		var pixels = target.Color.Download();
		AssertPixel( pixels, 32, 16, 16, 255, 0, 0, 255, "custom ImGui texture" );

		// The next NewFrame processes the deferred destruction.
		imgui.NewFrame( new EditorInput(), new Vector2( 32, 32 ), 1f / 60f );
		imgui.DiscardFrame();
		Assert.AreEqual( initialCount, imgui.TextureCount );
	}

	[TestMethod]
	public void EditorHostMapsStandardKeys()
	{
		Assert.AreEqual( ImGuiKey.A, EditorHost.MapKey( Key.A ) );
		Assert.AreEqual( ImGuiKey.Z, EditorHost.MapKey( Key.Z ) );
		Assert.AreEqual( ImGuiKey._0, EditorHost.MapKey( Key.Number0 ) );
		Assert.AreEqual( ImGuiKey._5, EditorHost.MapKey( Key.Number5 ) );
		Assert.AreEqual( ImGuiKey.F12, EditorHost.MapKey( Key.F12 ) );
		Assert.AreEqual( ImGuiKey.Enter, EditorHost.MapKey( Key.Enter ) );
		Assert.IsNull( EditorHost.MapKey( Key.Unknown ) );
	}

	private static void AssertPixel( byte[] pixels, int width, int x, int y, byte r, byte g, byte b, byte a, string what )
	{
		int i = (y * width + x) * 4;
		byte[] actual = [pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]];
		byte[] expected = [r, g, b, a];
		for ( int c = 0; c < 4; c++ )
		{
			if ( Math.Abs( actual[c] - expected[c] ) > 2 )
				Assert.Fail( $"{what}: expected {string.Join( ",", expected )}, got {string.Join( ",", actual )}" );
		}
	}
}
