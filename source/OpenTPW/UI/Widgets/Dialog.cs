using OpenTPW;
using OpenTPW.UI;

internal class Dialog : Panel
{
	private readonly Model OverlayModel;
	private readonly Model WindowModel;

	public Dialog()
	{
		OverlayModel = new Model( "ui/LOLIGHT.md2", Material.UIMesh );
		WindowModel = new Model( "ui/w_dialog_wave.md2", Material.UIMesh );
	}

	protected override void OnRender()
	{
		OverlayModel.Draw( Transform.Default );
		WindowModel.Draw( Transform.Default );
	}
}
