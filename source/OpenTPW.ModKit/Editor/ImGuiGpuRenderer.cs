using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTPW.Graphics;
using SDL;

namespace OpenTPW.ModKit;

/// <summary>
/// Draws ImGui.NET draw data through the shared SDL GPU layer. It owns the ImGui context, the font atlas
/// texture and every texture an editor panel shows, and records into the host's frame so uploads stay
/// ahead of the frame's draws like the rest of the game.
/// </summary>
public sealed unsafe class ImGuiGpuRenderer : IDisposable
{
	/// <summary>Size of ImGui's default <c>ImDrawVert</c> (two float2 and one packed colour).</summary>
	private const int VertexStride = 20;
	private const string ProgramName = "imgui";

	private readonly GpuDevice device;
	private readonly ShaderProgram program;
	private readonly GpuVertexLayout layout = new( VertexStride,
		new GpuVertexAttribute( 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 0 ),
		new GpuVertexAttribute( 1, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 8 ),
		new GpuVertexAttribute( 2, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_UBYTE4_NORM, 16 ) );
	private readonly Dictionary<IntPtr, GpuTexture> textures = new();
	// A panel can drop a texture after its image was already drawn this frame; the draw data still names it,
	// so destruction waits until the next NewFrame, when that draw data has been recorded.
	private readonly List<IntPtr> pendingDestroy = new();
	private readonly IntPtr context;
	private long nextId = 1;
	private bool disposed;

	/// <summary>Creates the renderer and the ImGui context. <paramref name="shaderDirectory"/> holds the precompiled <c>imgui</c> program.</summary>
	public ImGuiGpuRenderer( GpuDevice device, string shaderDirectory )
	{
		this.device = device;
		program = ShaderProgram.Load( device, shaderDirectory, ProgramName, new( UniformBuffers: 1 ), new( Samplers: 1 ) );
		context = ImGui.CreateContext();
		ImGui.SetCurrentContext( context );
		var io = ImGui.GetIO();
		// Large windows overflow 16-bit indices; the per-command vertex offset keeps them valid.
		io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
		CreateFontTexture( io );
	}

	private void CreateFontTexture( ImGuiIOPtr io )
	{
		io.Fonts.GetTexDataAsRGBA32( out byte* pixels, out int width, out int height, out int bytesPerPixel );
		var font = TexturePixels.Create( device, new ReadOnlySpan<byte>( pixels, width * height * bytesPerPixel ), (uint)width, (uint)height, mipmaps: false );
		io.Fonts.SetTexID( Register( font ) );
		io.Fonts.ClearTexData();
	}

	private IntPtr Register( GpuTexture texture )
	{
		var id = (IntPtr)nextId++;
		textures.Add( id, texture );
		return id;
	}

	/// <summary>Uploads RGBA8 pixels and returns the id to pass to <c>ImGui.Image</c>. Call <see cref="DestroyTexture"/> when done.</summary>
	public IntPtr CreateTexture( ReadOnlySpan<byte> rgba, uint width, uint height ) => Register( TexturePixels.Create( device, rgba, width, height, mipmaps: false ) );

	/// <summary>Schedules the texture for release once the current frame's draw data is no longer needed.</summary>
	public void DestroyTexture( IntPtr id )
	{
		if ( !textures.ContainsKey( id ) ) throw new ArgumentException( $"Unknown ImGui texture {id}.", nameof(id) );
		pendingDestroy.Add( id );
	}

	/// <summary>Number of live textures, including the font atlas.</summary>
	public int TextureCount => textures.Count;

	/// <summary>Applies the frame's input and starts an ImGui frame; panel code runs after this.</summary>
	public void NewFrame( EditorInput input, Vector2 displaySize, float deltaSeconds )
	{
		ObjectDisposedException.ThrowIf( disposed, this );
		ImGui.SetCurrentContext( context );
		foreach ( var id in pendingDestroy )
		{
			textures[id].Dispose();
			textures.Remove( id );
		}
		pendingDestroy.Clear();

		var io = ImGui.GetIO();
		io.DisplaySize = displaySize;
		// ImGui asserts on a zero or negative delta.
		io.DeltaTime = Math.Max( deltaSeconds, 1f / 1000f );
		io.AddMousePosEvent( input.MousePosition.X, input.MousePosition.Y );
		for ( int button = 0; button < input.MouseDown.Length; button++ ) io.AddMouseButtonEvent( button, input.MouseDown[button] );
		if ( input.WheelDelta != 0 ) io.AddMouseWheelEvent( 0, input.WheelDelta );
		foreach ( var (key, down) in input.Keys ) io.AddKeyEvent( key, down );
		foreach ( char c in input.Text ) io.AddInputCharacter( c );
		ImGui.NewFrame();
	}

	/// <summary>Ends the ImGui frame without drawing it (the editor is hidden but input must keep flowing).</summary>
	public void DiscardFrame()
	{
		ImGui.SetCurrentContext( context );
		ImGui.EndFrame();
	}

	/// <summary>Finishes the ImGui frame and records its draw data into <paramref name="pass"/>.</summary>
	public void Render( GpuFrame frame, GpuRenderPass pass )
	{
		ImGui.SetCurrentContext( context );
		ImGui.Render();
		var data = ImGui.GetDrawData();
		if ( data.TotalVtxCount == 0 || data.TotalIdxCount == 0 ) return;

		var vertices = new byte[data.TotalVtxCount * VertexStride];
		var indices = new byte[data.TotalIdxCount * sizeof(ushort)];
		int vertexBytes = 0, indexBytes = 0;
		for ( int i = 0; i < data.CmdListsCount; i++ )
		{
			var list = data.CmdListsRange[i];
			int vertexLength = list.VtxBuffer.Size * VertexStride, indexLength = list.IdxBuffer.Size * sizeof(ushort);
			new ReadOnlySpan<byte>( (void*)list.VtxBuffer.Data, vertexLength ).CopyTo( vertices.AsSpan( vertexBytes ) );
			new ReadOnlySpan<byte>( (void*)list.IdxBuffer.Data, indexLength ).CopyTo( indices.AsSpan( indexBytes ) );
			vertexBytes += vertexLength;
			indexBytes += indexLength;
		}
		var vertexSlice = frame.Upload<byte>( vertices );
		var indexSlice = frame.Upload<byte>( indices );

		var pipeline = device.GetPipeline( GpuPipelineDescription.For( pass.Target, program, layout ) with
		{
			Blend = GpuBlend.Alpha, DepthTest = false, DepthWrite = false, Cull = GpuCull.None
		} );
		var sampler = device.GetSampler( SamplerPreset.Linear );
		var binding = new TextureBinding[1];
		var displayPosition = data.DisplayPos;
		float width = data.DisplaySize.X * data.FramebufferScale.X, height = data.DisplaySize.Y * data.FramebufferScale.Y;
		var scaleTranslate = new Vector4( 2f / data.DisplaySize.X, -2f / data.DisplaySize.Y,
			-1f - displayPosition.X * 2f / data.DisplaySize.X, 1f + displayPosition.Y * 2f / data.DisplaySize.Y );

		uint indexOffset = 0;
		int vertexOffset = 0;
		for ( int i = 0; i < data.CmdListsCount; i++ )
		{
			var list = data.CmdListsRange[i];
			for ( int c = 0; c < list.CmdBuffer.Size; c++ )
			{
				var command = list.CmdBuffer[c];
				if ( command.UserCallback != IntPtr.Zero ) throw new NotSupportedException( "ImGui draw callbacks are not supported by the GPU renderer." );
				var clip = command.ClipRect;
				int x0 = (int)Math.Max( (clip.X - displayPosition.X) * data.FramebufferScale.X, 0 );
				int y0 = (int)Math.Max( (clip.Y - displayPosition.Y) * data.FramebufferScale.Y, 0 );
				int x1 = (int)Math.Min( (clip.Z - displayPosition.X) * data.FramebufferScale.X, width );
				int y1 = (int)Math.Min( (clip.W - displayPosition.Y) * data.FramebufferScale.Y, height );
				if ( x1 <= x0 || y1 <= y0 ) continue;
				if ( !textures.TryGetValue( command.TextureId, out var texture ) )
					throw new InvalidOperationException( $"ImGui draw refers to texture {command.TextureId}, which was never created or has been destroyed." );
				binding[0] = new TextureBinding( texture, sampler );
				pass.DrawIndexed( pipeline, vertexSlice, indexSlice, SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_16BIT, command.ElemCount,
					binding, GpuUniforms.Bytes( in scaleTranslate ), default,
					indexOffset + command.IdxOffset, vertexOffset + (int)command.VtxOffset, 1, new GpuScissor( x0, y0, x1 - x0, y1 - y0 ) );
			}
			indexOffset += (uint)list.IdxBuffer.Size;
			vertexOffset += list.VtxBuffer.Size;
		}
	}

	public void Dispose()
	{
		if ( disposed ) return;
		disposed = true;
		foreach ( var texture in textures.Values ) texture.Dispose();
		textures.Clear();
		program.Dispose();
		ImGui.DestroyContext( context );
	}
}
