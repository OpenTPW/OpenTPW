#if DEBUG
using System.Runtime.InteropServices;
using Veldrid;

namespace OpenTPW;

/// <summary>
/// Debug capture of the game's resolved render target: single frames for scene acceptance, or a run of
/// consecutive frames (<c>--capture-frames</c>) to check motion artefacts that a still image cannot show.
/// </summary>
internal sealed class FrameCapture
{
	private readonly string output;
	private readonly double warmUpSeconds;
	private readonly int count;
	private double started = -1;
	private int captured;

	internal FrameCapture( string output, double warmUpSeconds = 20, int count = 30 )
	{
		this.output = Directory.CreateDirectory( output ).FullName;
		this.warmUpSeconds = warmUpSeconds;
		this.count = count;
	}

	/// <summary>
	/// Called from the update of the next frame: by then the renderer has waited for the previous
	/// frame's fence, so the resolved target holds exactly that frame.
	/// </summary>
	internal void Update()
	{
		if ( started < 0 ) started = Time.Now;
		if ( Time.Now - started < warmUpSeconds ) return;
		SaveBmp( Path.Join( output, $"frame-{captured:D3}.bmp" ) );
		if ( ++captured < count ) return;
		Log.Info( $"Captured {count} consecutive frames to {output}" );
		Window.Current.SdlWindow.Close();
	}

	/// <summary>Writes the resolved render target as a top-down 32-bit BMP. Reads only the game's own target.</summary>
	internal static void SaveBmp( string path )
	{
		var source = Render.ResolveColorTexture;
		using var staging = Device.ResourceFactory.CreateTexture( TextureDescription.Texture2D( source.Width, source.Height,
			1, 1, PixelFormat.B8_G8_R8_A8_UNorm, TextureUsage.Staging ) );
		using var commands = Device.ResourceFactory.CreateCommandList();
		commands.Begin(); commands.CopyTexture( source, staging ); commands.End();
		Device.SubmitCommands( commands ); Device.WaitForIdle();
		var mapped = Device.Map( staging, MapMode.Read );
		try
		{
			int stride = checked((int)source.Width * 4), height = checked((int)source.Height);
			using var stream = File.Create( path );
			using var writer = new BinaryWriter( stream );
			// A negative DIB height preserves the top-first GPU rows without an extra image flip.
			writer.Write( (ushort)0x4d42 ); writer.Write( 54 + stride * height ); writer.Write( 0 ); writer.Write( 54 );
			writer.Write( 40 ); writer.Write( (int)source.Width ); writer.Write( -height );
			writer.Write( (ushort)1 ); writer.Write( (ushort)32 ); writer.Write( 0 ); writer.Write( stride * height );
			writer.Write( 0 ); writer.Write( 0 ); writer.Write( 0 ); writer.Write( 0 );
			var row = new byte[stride];
			for ( int y = 0; y < height; y++ ) { Marshal.Copy( mapped.Data + checked((int)(y * mapped.RowPitch)), row, 0, stride ); writer.Write( row ); }
		}
		finally { Device.Unmap( staging ); }
	}
}
#endif
