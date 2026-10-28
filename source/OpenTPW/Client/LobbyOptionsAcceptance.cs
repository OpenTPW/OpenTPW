#if DEBUG
using System.Text.Json;

namespace OpenTPW;

/// <summary>Exercise production options callbacks and capture the game's own target, without injecting OS input.</summary>
internal sealed class LobbyOptionsAcceptance
{
	private readonly Level level;
	private readonly string output;
	private readonly List<double> before = new(), after = new();
	private readonly int entityCount;
	private readonly LobbyOptionsSettings original = LobbyOptionsSettings.Current.Copy();
	private readonly BaseFileSystem originalSaves = SaveFileSystem;
	private int stage;
	private double changed = Time.Now;
	private int ticks;
	internal LobbyOptionsAcceptance( Level level, string output )
	{
		this.level = level; this.output = Directory.CreateDirectory( output ).FullName;
		entityCount = Entity.All.Count;
	}
	internal void Update()
	{
		if ( Time.Now - changed > 60 ) throw new TimeoutException( $"Options acceptance stage {stage}" );
		var options = level.Options!;
		switch ( stage )
		{
			case 0:
				if ( ++ticks < 90 ) { if ( ticks > 30 ) before.Add( Time.Delta * 1000 ); break; }
				level.Menu!.Show(); level.Menu.Choose( UIStrings.Options );
				Require( options.Active && level.Hud.Modal == options, "Escape Options opens modal" );
				stage++; ticks = 0; break;
			case 1:
				if ( ++ticks < 35 ) break;
				FrameCapture.SaveBmp( Path.Join( output, "lobby-options_opentpw.bmp" ) );
				int? voiceLimit = level.Audio?.Mixer.VoiceLimit;
				options.SetPercent( 0xd8, 12 ); options.Activate( 0xd9 ); options.SetPercent( 0xe0, 0 );
				Require( voiceLimit == level.Audio?.Mixer.VoiceLimit, "Quality draft does not evict playing voices" );
				options.SetPercent( 0xd6, 100 ); options.Cancel();
				Require( !options.Active && level.Hud.Modal == null, "Cancel restores modal routing" );
				Require( LobbyOptionsSettings.Current.Graphics == original.Graphics, "Cancel does not save draft quality" );
				// Switch between cached quality states while the scene continues updating for 30+ frames.
				LobbyQuality.Apply( "high" ); stage++; ticks = 0; break;
			case 2:
				// Exclude the capture readback and the first few frames after changing quality.
				if ( ticks > 5 ) after.Add( Time.Delta * 1000 );
				if ( ++ticks < 60 ) break;
				Require( Entity.All.Count == entityCount, "Quality changes do not construct scene entities" );
				LobbyQuality.Apply( "low" ); stage++; ticks = 0; break;
			case 3:
				if ( ++ticks < 35 ) break;
				// The OK path uses a private save root, so acceptance never overwrites a real profile.
				SaveFileSystem = new BaseFileSystem( Path.Join( output, "test-save" ) );
				options.PersistencePathOverride = Path.Join( output, Settings.FileName );
				level.Menu!.Show(); level.Menu.Choose( UIStrings.Options );
				options.SetPercent( 0xd6, 100 ); options.SetPercent( 0xd8, 37 ); options.Activate( -1 );
				stage++; ticks = 0; break;
			case 4:
				if ( options.Active ) break;
				Require( LobbyOptionsSettings.Current.Graphics == 2 && LobbyQuality.Load().Preset == "high", "OK applies quality" );
				var saved = Settings.Load( options.PersistencePathOverride! );
				Require( saved.LobbyQuality == "high" && saved.LobbyPreferences![4] == 37, "OK publishes global preferences" );
				File.WriteAllText( Path.Join( output, "options-acceptance.json" ), JsonSerializer.Serialize( new
				{
					passed = true, language = GameLanguage.Name, framesBefore = before, framesAfter = after,
					meanBefore = before.Average(), meanAfter = after.Average(), entityCount,
					checks = new[] { "Esc menu opens options", "Cancel restores routing", "Draft not saved", "Quality reuses resources", "OK applies quality", "OK publishes preferences" }
				}, new JsonSerializerOptions { WriteIndented = true } ) );
				SaveFileSystem = originalSaves; LobbyOptionsSettings.Accept( original );
				LobbyQuality.Apply( new[] { "low", "med", "high" }[original.Graphics] );
				Window.Current.SdlWindow.Close(); stage++; break;
		}
	}
	private static void Require( bool condition, string message )
	{
		if ( !condition ) throw new InvalidOperationException( "Options acceptance: " + message );
	}
}
#endif
