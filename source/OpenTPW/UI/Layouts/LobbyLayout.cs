using OpenTPW.UI;

namespace OpenTPW;

internal sealed class LobbyLayout : Layout
{
	private readonly PlayerProfiles profiles = new( SaveFileSystem.GetAbsolutePath( "" ) );
	private readonly PurpleButton[] playerButtons = new PurpleButton[PlayerProfiles.SlotCount];

	private sealed class MenuLogo : Image
	{
		public MenuLogo() : base( "/ui/textures/tpw_logo.wct" )
		{
			// tp_nocd.exe's tpwlogo.MD2 uses the lower atlas region and a 8:3 quad.
			Uv0 = new Vector2( 0.0005f, 0.5745f );
			Uv1 = new Vector2( 0.9995f, 0.9995f );
			Size = new Vector2( 180f, 67.5f );
		}

		protected override void OnRender()
		{
			Position = new Vector2( 25f, Screen.RawSize.Y - Size.Y - 25f );
			base.OnRender();
		}
	}

	public override void OnInit()
	{
		Hud.AddChild( new MenuLogo() );
		for ( int slot = 0; slot < playerButtons.Length; slot++ )
		{
			playerButtons[slot] = Hud.AddChild( new PurpleButton( "#CreateNewPlayer" ) { Position = new Vector2( Screen.RawSize.X, slot * 100 ) } );
		}

		RefreshPlayers();
		Hud.AddChild( new PurpleButton( "#QuitGame" )
		{
			Position = new Vector2( Screen.RawSize.X + 200, 425 ),
			Clicked = () => Hud.AddChild( new Dialog( "#ConfirmQuit", () => Window.Current.SdlWindow.Close() ) )
		} );
	}

	private void RefreshPlayers()
	{
		var players = profiles.Load();
		for ( int i = 0; i < playerButtons.Length; i++ )
		{
			int slot = i;
			var player = players.FirstOrDefault( profile => profile.Slot == slot );
			playerButtons[slot].Text = player?.Name ?? Localization.Parse( "#CreateNewPlayer" );
			playerButtons[slot].Clicked = () => SelectSlot( slot, player );
		}
	}

	private void SelectSlot( int slot, PlayerProfile? player )
	{
		if ( player != null )
		{
			LobbyScene.Current.SelectPlayer( player );
			return;
		}

		Hud.AddChild( new NewPlayerDialog( profiles, slot, OnPlayerCreated ) );
	}

	private void OnPlayerCreated( PlayerProfile player )
	{
		RefreshPlayers();
		LobbyScene.Current.SelectPlayer( player );
	}
}
