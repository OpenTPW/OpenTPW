using OpenTPW.UI;

namespace OpenTPW;

/// <summary>Black output rectangle from 0x573422..0x57346e.</summary>
internal sealed class LobbyFadePanel : Panel
{
	private readonly LobbyFade fade = new();
	private readonly Material material = new Material<ObjectUniformBuffer>( "content/shaders/ui.shader", MaterialFlags.DisableDepth | MaterialFlags.DisableCulling );
	private readonly Texture ramp;
	public LobbyFadePanel()
	{
		var pixels = new byte[256 * 4];
		for ( int alpha = 0; alpha < 256; alpha++ ) pixels[alpha * 4 + 3] = (byte)alpha;
		ramp = new Texture( pixels, 256, 1, TextureFlags.PointFilter );
	}
	protected override void OnUpdate() => fade.Update( LobbyCameraMode.EntryFade );
	protected override void OnRender()
	{
		if ( fade.Alpha == 0 ) return;
		material.Set( "Color", ramp );
		using ( new Graphics.Scope( new Point2( 1, 1 ) ) )
			Graphics.Quad( new Rectangle( 0, 0, 1, 1 ), new Rectangle( (fade.Alpha + 0.5f) / 256, 0.5f, 0, 0 ), material );
	}
}
