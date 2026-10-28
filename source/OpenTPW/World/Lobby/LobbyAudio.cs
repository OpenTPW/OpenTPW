namespace OpenTPW;

/// <summary>Page-scoped island music/SFX handles (TP.exe 0x5dc512/0x5dc536 and resume paths).</summary>
public sealed class LobbyAudio : IDisposable
{
	private readonly AudioSystem audio;
	private string? theme;
	private long music, ambience;
	public LobbyAudio( AudioSystem audio ) => this.audio = audio;
	/// <summary>Play the timed introduction cue from 0x595d30.</summary>
	public long IntroductionKey()
	{
		// 0x51b200 binds DAT_0081c16c to cat_ui; IDs are local to a category,
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
