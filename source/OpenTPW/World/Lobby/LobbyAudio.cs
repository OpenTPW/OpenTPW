namespace OpenTPW;

/// <summary>Page-scoped island music/SFX handles (TP.exe Lobby_StartIslandAudio (US 待核) 内，位置待核/Lobby_StartIslandAudio and resume paths).</summary>
public sealed class LobbyAudio : IDisposable
{
	private readonly AudioSystem audio;
	private string? theme;
	private long music, ambience;
	public LobbyAudio( AudioSystem audio ) => this.audio = audio;
	/// <summary>Play the timed introduction cue from FUN_00599880 (US 0x599880).</summary>
	public long IntroductionKey()
	{
		// Audio_RegisterGlobalCategories (US 0x51eae0) binds gUiAudioCategory (US 待核) to cat_ui; IDs are local to a category,
		// so 0xc6 must not be resolved through the four-entry lobby SFX bank.
		return audio.Play( "global/sound", "cat_ui", 0xc6 );
	}
	public void SetIsland( string nextTheme )
	{
		if ( theme == nextTheme ) return;
		// Each page owns these loops. Release both before selecting another island.
		Dispose();
		theme = nextTheme;
		music = audio.Play( $"levels/{theme}/Sound", "cat_locallobbymusic", 1 );
		ambience = audio.Play( $"levels/{theme}/Sound", "cat_locallobbysfx", 2 );
	}
	public void Dispose()
	{
		// Zero handles are safe, making page transitions and final disposal idempotent.
		audio.Stop( music ); audio.Stop( ambience );
		music = ambience = 0;
		theme = null;
	}
}
