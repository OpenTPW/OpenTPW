#if DEBUG
using System.Text.Json;

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
	/// <summary>Source input-frame index and capture clock for a consecutive resolved-frame readback.</summary>
	private sealed record FrameStamp( int Index, ulong SourceFrame, double CaptureSeconds );
	private readonly List<FrameStamp> stamps = [];

	internal FrameCapture( string output, double warmUpSeconds = 20, int count = 30 )
	{
		this.output = Directory.CreateDirectory( output ).FullName;
		this.warmUpSeconds = warmUpSeconds;
		this.count = count;
	}

	/// <summary>
	/// Called before recording the next frame. Download queues behind earlier submissions and waits,
	/// so the resolved target contains the last submitted frame even with multiple GPU frames in flight.
	/// </summary>
	internal void Update()
	{
		if ( started < 0 ) started = Time.Now;
		if ( Time.Now - started < warmUpSeconds ) return;
		SaveBmp( Path.Join( output, $"frame-{captured:D3}.bmp" ) );
		stamps.Add( new( captured, Input.FrameNumber - 1, Time.Now ) );
		if ( ++captured < count ) return;
		File.WriteAllText( Path.Join( output, "capture.json" ), JsonSerializer.Serialize( new
		{
			width = Render.ResolveColorTexture.Width, height = Render.ResolveColorTexture.Height,
			view = LobbyCameraMode.FixedViewPath, frames = stamps,
			assembly = typeof(FrameCapture).Assembly.ManifestModule.ModuleVersionId
		}, new JsonSerializerOptions { WriteIndented = true } ) );
		Log.Info( $"Captured {count} consecutive frames to {output}" );
		Window.Current.Close();
	}

	/// <summary>Writes the resolved render target as a top-down 32-bit BMP. Reads only the game's own target.</summary>
	internal static void SaveBmp( string path )
	{
		var source = Render.ResolveColorTexture;
		var pixels = source.Download();
		int stride = checked((int)source.Width * 4), height = checked((int)source.Height);
		using var stream = File.Create( path );
		using var writer = new BinaryWriter( stream );
		// The resolved target is BGRA8, so its top-first bytes directly match a negative-height BMP.
		writer.Write( (ushort)0x4d42 ); writer.Write( 54 + stride * height ); writer.Write( 0 ); writer.Write( 54 );
		writer.Write( 40 ); writer.Write( (int)source.Width ); writer.Write( -height );
		writer.Write( (ushort)1 ); writer.Write( (ushort)32 ); writer.Write( 0 ); writer.Write( stride * height );
		writer.Write( 0 ); writer.Write( 0 ); writer.Write( 0 ); writer.Write( 0 );
		writer.Write( pixels );
	}
}
#endif
