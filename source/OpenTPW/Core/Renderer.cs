using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Veldrid;
using Veldrid.StartupUtilities;

namespace OpenTPW;

public partial class Renderer
{
	private DateTime _lastFrame;
	private Dictionary<int, CommandList> _threadedCommandLists = new();
	public Window Window;
	public Action? PreUpdate;
	public Action? OnUpdate;
	public Action? PostUpdate;
	public Action? OnRender;
	public Action? OnRenderUI;

	public CommandList GetCommandList()
	{
		var thread = Environment.CurrentManagedThreadId;
		if ( !_threadedCommandLists.TryGetValue( thread, out var commandList ) )
		{
			commandList = Device.ResourceFactory.CreateCommandList();
			_threadedCommandLists[thread] = commandList;
		}

		return commandList;
	}

	public Renderer()
	{
		Window = new( Settings.Default.GameWindowSize.X, Settings.Default.GameWindowSize.Y, Localization.Parse( "#ThemeParkWorld" ), true );
		Window.OnResized = OnWindowResized;
		CreateGraphicsDevice();
		CreateMultisampledFramebuffer();
		CreateSamplers();
		_lastFrame = DateTime.Now;
		Window.Visible = true;
	}

	private void CreateSamplers()
	{
		Sampler.CreateSamplers();
	}

	[MemberNotNull( nameof( MultisampledFramebuffer ) )]
	private void CreateMultisampledFramebuffer()
	{
		var renderScale = 1.0f;
		var colorTextureInfo = TextureDescription.Texture2D( (uint)(Screen.Size.X * renderScale),
			(uint)(Screen.Size.Y * renderScale),
			1,
			1,
			PixelFormat.R10_G10_B10_A2_UNorm,
			TextureUsage.RenderTarget,
			TextureSampleCount.Count4 );
		var colorTexture = Device.ResourceFactory.CreateTexture( colorTextureInfo );
		var depthTextureInfo = TextureDescription.Texture2D( (uint)(Screen.Size.X * renderScale),
			(uint)(Screen.Size.Y * renderScale),
			1,
			1,
			PixelFormat.D24_UNorm_S8_UInt,
			TextureUsage.DepthStencil,
			TextureSampleCount.Count4 );
		var depthTexture = Device.ResourceFactory.CreateTexture( depthTextureInfo );
		var framebufferAttachmentInfo = new FramebufferAttachmentDescription( colorTexture, 0 );
		var depthAttachmentInfo = new FramebufferAttachmentDescription( depthTexture, 0 );
		var framebufferDescription = new FramebufferDescription()
		{
			ColorTargets = [framebufferAttachmentInfo],
			DepthTarget = depthAttachmentInfo
		};
		MultisampledFramebuffer = Device.ResourceFactory.CreateFramebuffer( framebufferDescription );
	}

	public Framebuffer MultisampledFramebuffer;

	public void Run()
	{
		OnWindowResized( Window.Size );
		while ( Window.SdlWindow.Exists )
		{
			Update();
		}

		Device.WaitForIdle();
		MsdfText.DisposeTextures();
	}

	private void BeginRender()
	{
		var shaders = Resource.All.ToArray().OfType<Shader>();
		foreach ( var shader in shaders.Where( x => x.IsDirty ) )
		{
			shader.Recompile();
		}

		var commandList = GetCommandList();
		commandList.Begin();
	}

	private void EndRender()
	{
		var commandList = GetCommandList();
		RenderWorld( commandList );
		RenderUI( commandList );
		commandList.PushDebugGroup( "Resolve" );
		commandList.ResolveTexture( MultisampledFramebuffer.ColorTargets[0].Target, Device.MainSwapchain.Framebuffer.ColorTargets[0].Target );
		commandList.PopDebugGroup();
		commandList.End();
		Device.SubmitCommands( commandList );
		Device.SwapBuffers();
	}

	private void RenderWorld( CommandList commandList )
	{
		commandList.PushDebugGroup( "Main Render" );
		commandList.SetFramebuffer( MultisampledFramebuffer );
		// Use MSAA framebuffer
		commandList.SetViewport( 0, new Viewport( 0, 0, MultisampledFramebuffer.Width, MultisampledFramebuffer.Height, 0, 1 ) );
		commandList.SetFullViewports();
		commandList.SetFullScissorRects();
		commandList.ClearDepthStencil( 1 );
		commandList.ClearColorTarget( 0, RgbaFloat.Black );
		Camera.Update();
		using ( var _ = new Graphics.Scope( (Point2)Screen.Size, Camera.Matrix ) )
		{
			OnRender?.Invoke();
		}

		commandList.PopDebugGroup();
	}

	private void RenderUI( CommandList commandList )
	{
		commandList.PushDebugGroup( "UI Render" );
		var uiMatrix = Matrix4x4.CreateOrthographic( 1280 * Screen.Aspect, 1280, 0.1f, 10000.0f );
		using ( var _ = new Graphics.Scope( (Point2)Screen.RawSize, uiMatrix ) )
		{
			OnRenderUI?.Invoke();
		}

		commandList.PopDebugGroup();
	}

	private void Update()
	{
		float deltaTime = (float)(DateTime.Now - _lastFrame).TotalSeconds;
		_lastFrame = DateTime.Now;
		InputSnapshot inputSnapshot = Window.SdlWindow.PumpEvents();
		Time.Update( deltaTime );
		Input.UpdateFrom( inputSnapshot );
		BeginRender();
		PreUpdate?.Invoke();
		OnUpdate?.Invoke();
		EndRender();
		PostUpdate?.Invoke();
		ProcessDeletionQueue();
	}

	private void CreateGraphicsDevice()
	{
		var options = new GraphicsDeviceOptions()
		{
			PreferStandardClipSpaceYDirection = true,
			PreferDepthRangeZeroToOne = true,
			SwapchainDepthFormat = null,
			SwapchainSrgbFormat = false,
			SyncToVerticalBlank = true,
			HasMainSwapchain = true
		};
		var swapchainSource = VeldridStartup.GetSwapchainSource( Window.SdlWindow );
		Device = GraphicsDevice.CreateVulkan( swapchainDescription: new SwapchainDescription( swapchainSource,
			(uint)(Window.Size.X),
			(uint)(Window.Size.Y),
			options.SwapchainDepthFormat,
			options.SyncToVerticalBlank,
			options.SwapchainSrgbFormat ), options: options );
	}

	public void OnWindowResized( Point2 newSize )
	{
		var dpiScale = 1.0f;
		Device.MainSwapchain.Resize( (uint)(newSize.X * dpiScale), (uint)(newSize.Y * dpiScale) );
		// Cleanup old MSAA resources
		MultisampledFramebuffer?.Dispose();
		// Recreate MSAA resources
		CreateMultisampledFramebuffer();
	}

	public void ImmediateSubmit( Action<CommandList> action )
	{
		var commandList = Device.ResourceFactory.CreateCommandList();
		commandList.Begin();
		action( commandList );
		commandList.End();
		Device.SubmitCommands( commandList );
		ScheduleDelete( commandList.Dispose );
	}
}
