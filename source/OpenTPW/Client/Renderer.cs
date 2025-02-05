using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Veldrid;
using Veldrid.StartupUtilities;

namespace OpenTPW;

public partial class Renderer
{
	private DateTime _lastFrame;
	public CommandList CommandList = null!;

	public Window Window;

	public Action? PreUpdate;
	public Action? OnUpdate;
	public Action? PostUpdate;

	public Action? OnRender;
	public Action? OnRenderUI;

	public Renderer()
	{
		Window = new( Settings.Default.GameWindowSize.X, Settings.Default.GameWindowSize.Y, Localization.Parse( "#ThemeParkWorld" ), true );
		Window.OnResized = OnWindowResized;

		CreateGraphicsDevice();
		CreateMultisampledFramebuffer();
		CreateBlitPipeline();
		CreateSamplers();

		CommandList = Device.ResourceFactory.CreateCommandList();
		_lastFrame = DateTime.Now;

		Window.Visible = true;
	}

	private void CreateSamplers()
	{
		Sampler.CreateSamplers();
	}

	[MemberNotNull( nameof( MultisampledFramebuffer ), nameof( ResolveColorTexture ) )]
	private void CreateMultisampledFramebuffer()
	{
		var colorTextureInfo = TextureDescription.Texture2D(
			(uint)(Screen.Size.X),
			(uint)(Screen.Size.Y),
			1,
			1,
			PixelFormat.B8_G8_R8_A8_UNorm,
			TextureUsage.RenderTarget,
			TextureSampleCount.Count4
		);

		var colorTexture = Device.ResourceFactory.CreateTexture( colorTextureInfo );

		var depthTextureInfo = TextureDescription.Texture2D(
			(uint)(Screen.Size.X),
			(uint)(Screen.Size.Y),
			1,
			1,
			PixelFormat.D32_Float_S8_UInt,
			TextureUsage.DepthStencil,
			TextureSampleCount.Count4
		);

		var depthTexture = Device.ResourceFactory.CreateTexture( depthTextureInfo );

		colorTextureInfo.SampleCount = TextureSampleCount.Count1;
		colorTextureInfo.Usage = TextureUsage.Sampled;

		ResolveColorTexture = Device.ResourceFactory.CreateTexture( colorTextureInfo );

		var framebufferAttachmentInfo = new FramebufferAttachmentDescription( colorTexture, 0 );
		var depthAttachmentInfo = new FramebufferAttachmentDescription( depthTexture, 0 );
		var framebufferDescription = new FramebufferDescription()
		{
			ColorTargets = [framebufferAttachmentInfo],
			DepthTarget = depthAttachmentInfo
		};

		MultisampledFramebuffer = Device.ResourceFactory.CreateFramebuffer( framebufferDescription );
	}

	[MemberNotNull( nameof( _blitResourceLayout ), nameof( _blitPipeline ), nameof( _blitResourceSet ) )]
	private void CreateBlitPipeline()
	{
		var layoutDescription = new ResourceLayoutDescription(
			new ResourceLayoutElementDescription( "g_tInput", ResourceKind.TextureReadOnly, ShaderStages.Fragment ),
			new ResourceLayoutElementDescription( "g_sSampler", ResourceKind.Sampler, ShaderStages.Fragment )
		);

		_blitResourceLayout = Device.ResourceFactory.CreateResourceLayout( layoutDescription );

		// Create shader
		var shader = new Shader( "content/shaders/blit.shader" );

		var pipelineDescription = new GraphicsPipelineDescription(
			BlendStateDescription.SingleAlphaBlend,
			DepthStencilStateDescription.Disabled,
			RasterizerStateDescription.CullNone,
			PrimitiveTopology.TriangleList,
			new ShaderSetDescription(
				Array.Empty<VertexLayoutDescription>(),
				shader.ShaderProgram
			),
			[_blitResourceLayout],
			Device.MainSwapchain.Framebuffer.OutputDescription
		);

		_blitPipeline = Device.ResourceFactory.CreateGraphicsPipeline( pipelineDescription );

		_blitResourceSet = Device.ResourceFactory.CreateResourceSet( new ResourceSetDescription(
			_blitResourceLayout,
			ResolveColorTexture,
			Device.LinearSampler
		) );
	}

	public Framebuffer MultisampledFramebuffer;
	public Veldrid.Texture ResolveColorTexture;

	private Pipeline _blitPipeline;
	private ResourceSet _blitResourceSet;
	private ResourceLayout _blitResourceLayout;

	public void Run()
	{
		OnWindowResized( Window.Size );

		while ( Window.SdlWindow.Exists )
		{
			Update();
		}
	}

	private void PreRender()
	{
		foreach ( var shader in Resource.All.OfType<Shader>().Where( x => x.IsDirty ) )
		{
			shader.Recompile();
		}

		CommandList.Begin();
	}

	private void PostRender()
	{
		CommandList.SetFramebuffer( MultisampledFramebuffer ); // Use MSAA framebuffer
		CommandList.SetViewport( 0, new Viewport( 0, 0, MultisampledFramebuffer.Width, MultisampledFramebuffer.Height, 0, 1 ) );
		CommandList.SetFullViewports();
		CommandList.SetFullScissorRects();
		CommandList.ClearDepthStencil( 1 );
		CommandList.ClearColorTarget( 0, RgbaFloat.Black );

		// Render level to MSAA buffer
		CommandList.PushDebugGroup( "Main Render" );

		Camera.Update();
		using ( var _ = new Graphics.Scope( (Point2)Screen.Size, Camera.Matrix ) )
		{
			OnRender?.Invoke();
		}

		CommandList.PopDebugGroup();

		CommandList.PushDebugGroup( "UI Render" );
		var uiMatrix = Matrix4x4.CreateOrthographic( 1280 * Screen.Aspect, 1280, 0.1f, 10000.0f );

		using ( var _ = new Graphics.Scope( (Point2)Screen.RawSize, uiMatrix ) )
		{
			OnRenderUI?.Invoke();
		}

		CommandList.PopDebugGroup();

		// Resolve MSAA to non-MSAA texture
		CommandList.PushDebugGroup( "Resolve and Blit" );
		CommandList.ResolveTexture( MultisampledFramebuffer.ColorTargets[0].Target, ResolveColorTexture );

		// Blit to screen
		CommandList.SetFramebuffer( Device.MainSwapchain.Framebuffer );
		CommandList.SetViewport( 0, new Viewport( 0, 0, Device.MainSwapchain.Framebuffer.Width, Device.MainSwapchain.Framebuffer.Height, 0, 1 ) );

		CommandList.SetPipeline( _blitPipeline );
		CommandList.SetGraphicsResourceSet( 0, _blitResourceSet );
		CommandList.Draw( 3, 1, 0, 0 );
		CommandList.PopDebugGroup();

		CommandList.End();

		Device.SubmitCommands( CommandList );
		Device.SwapBuffers();
	}

	private void Update()
	{
		float deltaTime = (float)(DateTime.Now - _lastFrame).TotalSeconds;
		_lastFrame = DateTime.Now;

		InputSnapshot inputSnapshot = Window.SdlWindow.PumpEvents();

		Time.Update( deltaTime );
		Input.UpdateFrom( inputSnapshot );

		PreRender();
		PreUpdate?.Invoke();

		OnUpdate?.Invoke();

		PostRender();
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
		Device = GraphicsDevice.CreateVulkan( swapchainDescription: new SwapchainDescription( swapchainSource, (uint)(Window.Size.X), (uint)(Window.Size.Y), options.SwapchainDepthFormat, options.SyncToVerticalBlank, options.SwapchainSrgbFormat ), options: options );
	}

	public void OnWindowResized( Point2 newSize )
	{
		var dpiScale = 1.0f;
		Device.MainSwapchain.Resize( (uint)(newSize.X * dpiScale), (uint)(newSize.Y * dpiScale) );

		// Cleanup old MSAA resources
		MultisampledFramebuffer?.Dispose();
		ResolveColorTexture?.Dispose();

		// Recreate MSAA resources
		CreateMultisampledFramebuffer();

		// Recreate blit resources since they depend on the framebuffer
		_blitResourceSet?.Dispose();
		_blitResourceSet = Device.ResourceFactory.CreateResourceSet( new ResourceSetDescription(
			_blitResourceLayout,
			ResolveColorTexture,
			Device.LinearSampler
		) );
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
