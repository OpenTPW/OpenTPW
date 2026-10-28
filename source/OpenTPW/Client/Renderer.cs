using Veldrid;
using Veldrid.StartupUtilities;

namespace OpenTPW;

public partial class Renderer
{
	private DateTime _lastFrame;
	public CommandList CommandList = null!;

	public Window Window;
	public ImGuiRenderer imGuiRenderer;

	public Action? PreUpdate;
	public Action? OnUpdate;
	public Action? PostUpdate;

	public Action? OnRender;

	public Renderer()
	{
		Window = new( Settings.Default.GameWindowSize.X, Settings.Default.GameWindowSize.Y, "Theme Park World", true );
		Window?.OnResized = OnWindowResized;
		Window?.Visible = true;

		CreateGraphicsDevice();
		// Swap the buffers so that the screen isn't a mangled mess
		Device.SwapBuffers();
		CreateMultisampledFramebuffer();

		imGuiRenderer = new ImGuiRenderer( Device, Device.MainSwapchain.Framebuffer.OutputDescription, Window.Size.X, Window.Size.Y );
		ModKit.GlobalNamespace.ImGuiManager = imGuiRenderer;
		new Editor( imGuiRenderer, Device );

		CommandList = Device.ResourceFactory.CreateCommandList();
		_lastFrame = DateTime.Now;
	}

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

	public Framebuffer MultisampledFramebuffer;
	public Veldrid.Texture ResolveColorTexture;

	private Pipeline _blitPipeline;
	private ResourceSet _blitResourceSet;
	private ResourceLayout _blitResourceLayout;

	public void Run()
	{
		var layoutDescription = new ResourceLayoutDescription(
			new ResourceLayoutElementDescription( "g_tInput", ResourceKind.TextureReadOnly, ShaderStages.Fragment ),
			new ResourceLayoutElementDescription( "g_sSampler", ResourceKind.Sampler, ShaderStages.Fragment )
		);

		_blitResourceLayout = Device.ResourceFactory.CreateResourceLayout( layoutDescription );

		// Create shader
		var shader = new Shader( "content/shaders/blit.shader" );
		var blitShader = shader.ShaderProgram;

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

		OnWindowResized( Window.Size );

		while ( Window.SdlWindow.Exists )
		{
			Update();
		}
	}

	private void PreRender()
	{
		foreach ( var shader in Asset.All.OfType<Shader>().Where( x => x.IsDirty ) )
		{
			shader.Recompile();
		}

		CommandList.Begin();
	}

	private void PostRender( double frameBegin )
	{
		CommandList.SetFramebuffer( MultisampledFramebuffer ); // Use MSAA framebuffer
		CommandList.SetViewport( 0, new Viewport( 0, 0, MultisampledFramebuffer.Width, MultisampledFramebuffer.Height, 0, 1 ) );
		CommandList.SetFullViewports();
		CommandList.SetFullScissorRects();
		CommandList.ClearDepthStencil( 1 );
		CommandList.ClearColorTarget( 0, RgbaFloat.Black );

		// Render level to MSAA buffer
		CommandList.PushDebugGroup( "Main Render" );
		double renderBegin = Mark();
		OnRender?.Invoke();
		AddRender( Mark() - renderBegin );
		CommandList.PopDebugGroup();

		// Resolve MSAA to non-MSAA texture
		CommandList.ResolveTexture( MultisampledFramebuffer.ColorTargets[0].Target, ResolveColorTexture );

		// Blit to screen
		CommandList.SetFramebuffer( Device.MainSwapchain.Framebuffer );
		CommandList.SetViewport( 0, new Viewport( 0, 0, Device.MainSwapchain.Framebuffer.Width, Device.MainSwapchain.Framebuffer.Height, 0, 1 ) );

		CommandList.SetPipeline( _blitPipeline );
		CommandList.SetGraphicsResourceSet( 0, _blitResourceSet );
		CommandList.Draw( 3, 1, 0, 0 );

		Editor.Instance?.Render( CommandList );

		CommandList.End();

		Submissions++;
		FrameEnd( frameBegin );
		double presentBegin = Mark();
		SubmitFrame( CommandList );
		Device.SwapBuffers();
		AddPresent( Mark() - presentBegin );
	}

	private void Update()
	{
		double frameBegin = FrameBegin();
		float deltaTime = (float)(DateTime.Now - _lastFrame).TotalSeconds;
		_lastFrame = DateTime.Now;

		InputSnapshot inputSnapshot = Window.SdlWindow.PumpEvents();
		if ( !Window.SdlWindow.Exists ) return;

		Time.Update( deltaTime );
		Input.UpdateFrom( inputSnapshot );

		if ( Input.Pressed( InputButton.EditorToggle ) )
			Editor.Instance.shouldRender = !Editor.Instance.shouldRender;
		if ( Editor.Instance.shouldRender )
			Editor.Instance.UpdateFrom( inputSnapshot );
		else
			imGuiRenderer.Update( Math.Max( deltaTime, 0.001f ), inputSnapshot );

		// Updates may rewrite vertex buffers and textures the previous frame was drawing with.
		BeginFrame();
		PreUpdate?.Invoke();

		double updateBegin = Mark();
		OnUpdate?.Invoke();
		AddUpdate( Mark() - updateBegin );
		// A HUD callback can close the native window during this update.
		if ( !Window.SdlWindow.Exists ) return;

		PreRender();
		PostRender( frameBegin );
		PostUpdate?.Invoke();
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

		imGuiRenderer?.WindowResized( newSize.X, newSize.Y );
	}

	public void ImmediateSubmit( Action<CommandList> action )
	{
		var commandList = Device.ResourceFactory.CreateCommandList();
		commandList.Begin();

		action( commandList );

		commandList.End();
		Submissions++;
		Device.SubmitCommands( commandList );

		ScheduleDelete( commandList.Dispose );
	}
}
