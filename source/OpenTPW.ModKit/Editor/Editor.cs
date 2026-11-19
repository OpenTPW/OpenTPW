using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTPW.Graphics;
using OpenTPW.ModKit;
using SDL;
using static SDL.SDL3;

namespace OpenTPW;
public partial class Editor
{
	public static Editor Instance { get; private set; }

	public ImGuiGpuRenderer Renderer { get; private set; }

	private List<BaseTab> tabs = new();

	public bool shouldRender = false;

	public static ImFontPtr MonospaceFont { get; private set; }
	public static ImFontPtr SansSerifFont { get; private set; }

	public Editor( ImGuiGpuRenderer renderer )
	{
		Instance ??= this;
		Renderer = renderer;

		InitIO();

		tabs.AddRange( new BaseTab[] {
			new DemoWindow(),
			new FileBrowser()
		} );
	}

	private void InitIO()
	{
		ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
	}
	private void DrawMenuBar()
	{
		ImGui.BeginMainMenuBar();

		foreach ( var tab in tabs )
		{
			var editorMenuAttribute = tab.GetType().GetCustomAttribute<EditorMenuAttribute>();
			if ( editorMenuAttribute == null )
				continue;

			var splitPath = editorMenuAttribute.Path.Split( '/' );

			if ( ImGui.BeginMenu( splitPath[0] ) )
			{
				for ( int i = 1; i < splitPath.Length; i++ )
				{
					string? item = splitPath[i];
					bool active = ImGui.MenuItem( item );

					if ( i == splitPath.Length - 1 && active )
						tab.visible = !tab.visible;
				}

				ImGui.EndMenu();
			}
		}

		ImGui.EndMainMenuBar();
	}

	/// <summary>Starts the editor's ImGui frame from the host's input and builds every visible panel.</summary>
	public void UpdateFrom( EditorInput input, Vector2 displaySize, float deltaSeconds )
	{
		Renderer.NewFrame( input, displaySize, deltaSeconds );

		DrawMenuBar();

		ImGui.DockSpaceOverViewport( ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode );

		tabs.ForEach( tab =>
		{
			if ( tab.visible )
				tab.Draw();
		} );
	}

	public async Task<string> CacheThumbnailAsync( string path )
	{
		if ( !path.EndsWith( ".wct" ) )
			return "";

		var thumbnailPath = FileSystem.GetRelativePath( path );
		thumbnailPath = Path.ChangeExtension( thumbnailPath, ".png" );
		thumbnailPath = "/thumbcache" + thumbnailPath;

		await Task.Run( () =>
		{
			var textureFile = new TextureFile( path );
			var t = textureFile.Data;

			var pixelData = ResizeThumbnail( t.Data, t.Width, t.Height );
			using var writeStream = CacheFileSystem.OpenWrite( thumbnailPath );
			var png = EncodePng( pixelData, 16, 16 );
			writeStream.Write( png );

			Log.Info( $"Cached to {thumbnailPath}" );
		} );

		return thumbnailPath;
	}

	/// <summary>Reduces an RGBA image to a 16 by 16 thumbnail by averaging source pixels.</summary>
	internal static byte[] ResizeThumbnail( byte[] pixels, int width, int height )
	{
		if ( width <= 0 || height <= 0 || (long)width * height * 4 != pixels.Length )
			throw new ArgumentException( "Invalid RGBA image dimensions or pixel data." );

		var result = new byte[16 * 16 * 4];
		for ( int y = 0; y < 16; y++ )
		for ( int x = 0; x < 16; x++ )
		{
			int x0 = (int)((long)x * width / 16), x1 = Math.Max( x0 + 1, (int)((long)(x + 1) * width / 16) );
			int y0 = (int)((long)y * height / 16), y1 = Math.Max( y0 + 1, (int)((long)(y + 1) * height / 16) );
			long count = (long)(x1 - x0) * (y1 - y0);
			for ( int channel = 0; channel < 4; channel++ )
			{
				long sum = 0;
				for ( int sy = y0; sy < y1; sy++ )
				for ( int sx = x0; sx < x1; sx++ )
					sum += pixels[(sy * width + sx) * 4 + channel];
				result[(y * 16 + x) * 4 + channel] = (byte)(sum / count);
			}
		}
		return result;
	}

	/// <summary>Records the panels built by <see cref="UpdateFrom"/> into the host's frame.</summary>
	public void Render( GpuFrame frame, GpuRenderPass pass ) => Renderer.Render( frame, pass );

	/// <summary>Encodes RGBA8 pixels as a PNG with SDL's surface writer, which the game already ships.</summary>
	internal static unsafe byte[] EncodePng( byte[] rgba, int width, int height )
	{
		if ( width <= 0 || height <= 0 || (long)width * height * 4 != rgba.Length )
			throw new ArgumentException( "Invalid RGBA image dimensions or pixel data." );
		// ABGR8888 is SDL's RGBA32: bytes R,G,B,A in memory on little-endian hosts. SDL_SavePNG_IO needs a stream and cannot grow a memory one. A thumbnail is at most 16x16, so this holds
		// raw pixels plus any PNG overhead many times over; a full buffer makes SDL fail rather than truncate.
		var output = new byte[64 * 1024];
		long length;
		fixed ( byte* pixels = rgba )
		fixed ( byte* destination = output )
		{
			var surface = SDL_CreateSurfaceFrom( width, height, SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888, (IntPtr)pixels, width * 4 );
			if ( surface == null ) throw new InvalidOperationException( $"SDL_CreateSurfaceFrom failed: {SDL_GetError()}" );
			try
			{
				var stream = SDL_IOFromMem( (IntPtr)destination, (nuint)output.Length );
				if ( stream == null ) throw new InvalidOperationException( $"SDL_IOFromMem failed: {SDL_GetError()}" );
				try
				{
					if ( !SDL_SavePNG_IO( surface, stream, false ) ) throw new InvalidOperationException( $"SDL_SavePNG_IO failed: {SDL_GetError()}" );
					length = SDL_TellIO( stream );
				}
				finally { SDL_CloseIO( stream ); }
			}
			finally { SDL_DestroySurface( surface ); }
		}
		return output[..(int)length];	}
}

