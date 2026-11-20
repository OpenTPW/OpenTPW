namespace OpenTPW;

/// <summary>Lobby HUD entry: the player page is the first page shown (Lobby_CreateUi (US 0x4a6580)); other lobby pages are created by <see cref="Level"/>.</summary>
internal sealed class LobbyLayout : Layout
{
	public override void OnInit()
	{
		var players = new LobbyPlayerPanel( Level.Current );
		Level.Current.PlayerMenu = players;
		Hud.AddChild( players );
	}
}
