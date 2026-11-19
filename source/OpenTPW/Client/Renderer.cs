using OpenTPW.Graphics;
using SDL;

namespace OpenTPW;

/// <summary>SDL GPU frame loop; uploads during update precede frame recording, and the shared device owns all fences.</summary>
public sealed partial class Renderer : IDisposable
{
	private DateTime lastFrame = DateTime.Now;
	public Window Window { get; }
	public RenderTarget Target { get; }
	public GpuTexture ResolveColorTexture => Target.Color;
	internal GpuFrame Frame { get; private set; } = null!;
	internal GpuRenderPass Pass { get; private set; } = null!;
	public Action? PreUpdate, OnUpdate, PostUpdate, OnRender, PrepareResources;
	/// <summary>Plays a game file for the editor's sound preview; set by the host that owns the audio output.</summary>
	public Func<string, long>? PreviewSound { get; set; }
	private EditorHost? editor;
	public unsafe Renderer()
	{
		Window = new( Settings.Default.GameWindowSize.X, Settings.Default.GameWindowSize.Y, "Theme Park World", true );
		GpuDevice? created = null;
		try
		{
			Device = created = GpuDevice.Create();
			Device.AttachWindow( Window.Handle );
			var format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_B8G8R8A8_UNORM;
			var maximum = Device.MaxSampleCount( format );
			var samples = maximum >= SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_4 ? SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_4 : maximum;
			Target = new( Device, (uint)Window.Size.X, (uint)Window.Size.Y, format, samples );
			Window.OnResized = OnWindowResized;
			Window.Visible = true;
			Log.Info( $"SDL GPU renderer: {Device.Driver}, {Device.ShaderFormat}, {Window.Size.X}x{Window.Size.Y} physical pixels" );
		}
		// A failed claim or target creation must release the device before destroying its window.
		catch { created?.Dispose(); Window.Dispose(); throw; }
	}
	public void Run() { lastFrame = DateTime.Now; while ( Window.Exists ) Update(); }
	private void Update()
	{
		double begin = FrameBegin();
		float delta = (float)(DateTime.Now - lastFrame).TotalSeconds;
		lastFrame = DateTime.Now;
		ProcessDeletionQueue();
		var input = Window.PumpEvents();
		if ( !Window.Exists ) return;
		Time.Update( delta ); Input.UpdateFrom( input );
		if ( Input.Pressed( InputButton.EditorToggle ) ) ( editor ??= new EditorHost( Device ) ).Toggle( PreviewSound );
		PreUpdate?.Invoke();
		double updated = Mark(); OnUpdate?.Invoke();
		if ( !Window.Exists ) return;
		PrepareResources?.Invoke();
		// Panels build their ImGui frame, and upload any textures, before the frame starts recording.
		if ( editor is { Visible: true } ) editor.Update( input, new( Target.Color.Width, Target.Color.Height ), delta );
		AddUpdate( Mark() - updated );
		double wait = Mark(); Frame = Device.BeginFrame(); AddWait( Mark() - wait );
		Pass = Frame.BeginPass( Target, GpuClear.ToColor( 0, 0, 0, 1 ) );
		try
		{
			double draw = Mark(); OnRender?.Invoke(); if ( editor is { Visible: true } ) editor.Render( Frame, Pass ); AddRender( Mark() - draw );
			Pass.End(); Frame.Present( Target.Color );
			double present = Mark(); Frame.Submit(); AddPresent( Mark() - present );
		}
		// Discard CPU-side draw records on a scene failure so shutdown cannot replace the original exception.
		catch { Frame.Cancel(); throw; }
		Submissions++; FrameEnd( begin ); PostUpdate?.Invoke();
	}
	public void OnWindowResized( Point2 size ) { if ( size.X > 0 && size.Y > 0 ) Target.Resize( (uint)size.X, (uint)size.Y ); }
	public void Dispose()
	{
		Device.WaitForIdle();
		editor?.Dispose();
		foreach ( var mesh in Asset.All.OfType<Model>() ) mesh.Geometry.Dispose();
		foreach ( var texture in Asset.All.OfType<Texture>().Select( t => t.NativeTexture ).Distinct() ) texture.Dispose();
		Shader.ReleasePrograms(); Target.Dispose(); Device.Dispose();
	}
}
