using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

/// <summary>US options table 0x752f30: fixed control bounds, live audio preview, OK commit and Cancel rollback.</summary>
internal sealed class LobbyOptionsPanel : Panel
{
	/// <summary>A preloaded native model control and its finite set of pre-rasterized labels.</summary>
	private sealed class Control
	{
		internal int Id;
		internal Rectangle Bounds;
		internal LobbyUiModel Model = null!;
		internal LobbyUiText? Label;
		internal LobbyUiModel? Handle;
		internal float HandleY;
	}
	private readonly Level level;
	private readonly List<Control> controls = new();
	private readonly Control[] interactive;
	private readonly LobbyUiModel background;
	private readonly LobbyUiText title, status, language, pack;
	private readonly string[] languages, packs;
	private readonly string[] baseLanguages;
	private readonly byte[] defaultPreferences;
	private readonly Dictionary<string, string[]> packLanguages = new( StringComparer.OrdinalIgnoreCase );
	private LobbyOptionsSettings draft = null!;
	private PlayerProfileFile preferences = null!;
	private byte[] originalPreferences = null!;
	private Task? saving;
	private ulong opened;
	private int dragging = -1, focused;
	internal bool Active { get; private set; }
#if DEBUG
	/// <summary>Acceptance writes to a private settings file rather than the executable's real configuration.</summary>
	internal string? PersistencePathOverride { get; set; }
#endif
	private static Rectangle Box( float l, float t, float r, float b ) => LobbyNamingPanel.Box( l, t, r, b );
	internal LobbyOptionsPanel( Level level )
	{
		this.level = level;
		defaultPreferences = PlayerProfileFile.DefaultPreferences( new SettingsFile( "sound.sam" ) );
		baseLanguages = GameLanguage.Available( new BaseFileSystem( Path.Combine( GameDir.GamePath, "data" ) ) );
		LobbyUiModel Model( uint hash, Rectangle bounds )
		{
			var model = new LobbyUiModel( LobbyControlResources.ResolveModel( hash ), bounds );
			model.PreloadAll(); return model;
		}
		LobbyUiText Text( Rectangle bounds, IEnumerable<string> values )
		{
			// US 0x4a43b0 reads font slot 6 from 0x7537b4 and sets left alignment and opaque black.
			var text = new LobbyUiText( "", bounds, LobbyFonts.Name( 6 ), 0x000000ff );
			text.HorizontalAlignment = 0; text.Preload( values ); return text;
		}
		string T( UIStrings key ) => Localization.Text( key );
		background = Model( 0xc8a6dfaf, Box( 0, 0, 2048, 1536 ) );
		// US 0x485d20 selects font slot 5 (record 0x78b5c4); 0x492d20 sets RGBA ea/ef/66/ff.
		title = new LobbyUiText( T( UIStrings.GameOptions ), Box( 788, 46, 1260, 126 ), LobbyFonts.Name( 5 ), 0xeaef66ff ) { Shadow = true };
		title.Preload( new[] { title.Text } );
		status = Text( Box( 57, 1465, 1964, 1530 ), new[] { "", "Saving...", "Save failed. Retry OK or Cancel.",
			"Language changes require a restart.", "Rendering backend and video card are managed by OpenTPW.",
			"Resolution changes require a restart." } );
		languages = GameLanguage.Available( FileSystem ).OrderBy( value => value, StringComparer.OrdinalIgnoreCase ).ToArray();
		// The configured pack is always selectable; discover sibling installations and packs once,
		// before opening the page. A pack selection never mutates the running filesystem overlay.
		var candidates = new List<string> { "", Settings.Default.LanguagePack };
		var roots = new List<string?> { Path.GetDirectoryName( GameDir.GamePath ) };
		// Test mirrors may junction Language/ into the original installation. Follow that metadata
		// once to discover adjacent packs without embedding a machine-specific games directory.
		var languageRoot = new DirectoryInfo( Path.Combine( GameDir.GamePath, "data", "Language" ) );
		if ( languageRoot.LinkTarget != null && languageRoot.ResolveLinkTarget( true ) is DirectoryInfo target )
			roots.Add( target.Parent?.Parent?.Parent?.FullName );
		foreach ( string parent in roots.OfType<string>().Distinct( StringComparer.OrdinalIgnoreCase ) )
			foreach ( string directory in Directory.GetDirectories( parent ) )
			{
				if ( Directory.Exists( Path.Combine( directory, "data", "Language" ) ) ) candidates.Add( directory );
				else if ( !Directory.Exists( Path.Combine( directory, "data" ) ) )
					foreach ( string child in Directory.GetDirectories( directory ) )
						if ( Directory.Exists( Path.Combine( child, "data", "Language" ) ) ) candidates.Add( child );
			}
		packs = candidates.Distinct( StringComparer.OrdinalIgnoreCase ).OrderBy( value => value ).ToArray();
		foreach ( string candidate in packs ) packLanguages.Add( candidate, candidate.Length == 0 ? baseLanguages :
			GameLanguage.PackLanguages( GameLanguage.PackDataFolder( candidate ) ) );
		languages = languages.Concat( packs.Where( value => value.Length != 0 ).SelectMany( value =>
			packLanguages[value] ) ).Distinct( StringComparer.OrdinalIgnoreCase ).OrderBy( value => value ).ToArray();
		language = Text( Box( 1331, 20, 1964, 87 ), languages.Select( value => "Language: " + value ) );
		pack = Text( Box( 1331, 87, 1964, 154 ), packs.Select( PackLabel ) );
		void Add( int id, uint hash, Rectangle bounds, Rectangle? labelBounds = null, IEnumerable<string>? labels = null )
		{
			controls.Add( new Control { Id = id, Bounds = bounds, Model = Model( hash, bounds ),
				Label = labelBounds.HasValue ? Text( labelBounds.Value, labels! ) : null } );
		}
		void Caption( int id, float l, float t, float r, float b, float x, float y, params UIStrings[] labels ) =>
			Add( id, 0xcea38960, Box( l, t, r, b ), Box( x, y, x + 300, y + 45 ), labels.SelectMany( key => new[] { T( key ), T( key ) + T( UIStrings.On ), T( key ) + T( UIStrings.Off ),
				T( key ) + T( UIStrings.Primary ), T( key ) + T( UIStrings.Smooth ), T( key ) + T( UIStrings.Rotation90Degs ),
				T( key ) + T( UIStrings.Pushscroll ), T( key ) + T( UIStrings.RightButton ) } ) );
		Caption( 0xc8, 57, 167, 668, 316, 124, 218, UIStrings.GPURendering );
		Caption( 0xca, 675, 167, 1286, 316, 739, 218, UIStrings.Videocard );
		var names = new[] { UIStrings.Advisor, UIStrings.Tutorial, UIStrings.PopupHelp, UIStrings.Confirmations,
			UIStrings.RmbCancel, UIStrings.Rotation, UIStrings.Scroll };
		int[] tops = { 167, 323, 479, 681, 837, 994, 1150 };
		int[] textY = { 215, 371, 528, 732, 888, 1044, 1200 };
		int[] buttonY = { 190, 346, 503, 705, 861, 1017, 1173 };
		for ( int i = 0; i < 7; i++ )
		{
			Caption( 0xcb + i, 1331, tops[i], 1964, tops[i] + 149, 1401, textY[i], names[i] );
			Add( 0xc1 + i, i < 5 ? 0x3aec1u : 0x211ef0a0u,
				Box( i < 5 ? 1786 : 1807, buttonY[i], i < 5 ? 1930 : 1910, buttonY[i] + 102 ) );
		}
		// Hardware/software and adapter selection have no equivalent in the current Vulkan renderer.
		Add( 0xd2, 0x211ef0a0, Box( 521, 190, 624, 292 ) );
		Add( 0xd3, 0x211ef0a0, Box( 1140, 190, 1242, 292 ) );
		void Slider( int id, int top, UIStrings name, IEnumerable<string> values, bool audio )
		{
			Add( id, audio ? 0xcea389edu : 0xcea389beu, Box( audio ? 52 : 57, top, 1289, top + 150 ),
				Box( 117, top + 52, 706, top + 97 ), values.Select( value => T( name ) + " " + value.Trim() ) );
			var control = controls[^1]; control.HandleY = top + 41;
			control.Handle = Model( 0x86cb3e31, Box( 1023, top + 41, 1090, top + 109 ) );
		}
		Slider( 0xd5, 323, UIStrings.ScreenResolution, new[] { T( UIStrings.Custom ), T( UIStrings.Resolution512x384 ), T( UIStrings.Resolution640x480 ), T( UIStrings.Resolution800x600 ) }, false );
		Slider( 0xd6, 479, UIStrings.GraphicsQuality, new[] { T( UIStrings.Low ), T( UIStrings.Medium ), T( UIStrings.High ) }, false );
		var percentages = Enumerable.Range( 0, 101 ).Select( value => value + " %" ).ToArray();
		Slider( 0xe0, 638, UIStrings.AudioQuality, percentages, false );
		var audioNames = new[] { UIStrings.SoundEffectsVolume, UIStrings.MusicVolume, UIStrings.SpeechVolume, UIStrings.MovieVolume };
		for ( int i = 0; i < 4; i++ )
		{
			int top = 838 + i * 156 + (i >= 2 ? 1 : 0);
			Slider( 0xd8 + i * 2, top, audioNames[i], percentages.Append( T( UIStrings.Off ) ), true );
			Add( 0xd7 + i * 2, 0x3aec1, Box( 801, top + 24, 945, top + 126 ) );
		}
		Add( 0xdf, 0xbafbd0f8, Box( 1742, 1321, 1947, 1443 ) );
		Add( -2, 0x16ac3f4f, Box( 1853, 1340, 1936, 1424 ) );
		Add( -1, 0x14a7e48b, Box( 1763, 1340, 1846, 1424 ) );
		interactive = controls.Where( c => c.Handle != null || c.Id is >= 0xc1 and <= 0xc7 || c.Id is 0xd2 or 0xd3 or 0xd7 or 0xd9 or 0xdb or 0xdd or -1 or -2 ).ToArray();
	}
	/// <summary>Distinguish equally named installed/CD packs using cached paths, without directory reads in the UI.</summary>
	private string PackLabel( string value )
	{
		if ( value.Length == 0 ) return "Pack: None";
		string name = Path.GetFileName( value );
		if ( packs.Count( candidate => Path.GetFileName( candidate ).Equals( name, StringComparison.OrdinalIgnoreCase ) ) > 1 )
			name = Path.GetFileName( Path.GetDirectoryName( value ) ) + "/" + name;
		return "Pack: " + name;
	}
	internal void Show()
	{
		if ( Active || level.Hud.Modal != null ) return;
		draft = LobbyOptionsSettings.Current.Copy();
		if ( draft.Language.Length != 0 ) draft.Language = languages.First( value => value.Equals( draft.Language, StringComparison.OrdinalIgnoreCase ) );
		originalPreferences = (byte[])(level.PlayerMenu?.Profile?.Preferences ?? level.Audio?.SnapshotPreferences() ?? LobbyOptionsSettings.Current.Preferences ?? defaultPreferences).Clone();
		preferences = PlayerProfileFile.Create( 0, originalPreferences );
		Active = true; opened = Input.FrameNumber; focused = 0; dragging = -1; status.Text = "";
		level.Hud.Modal = this; Refresh();
	}
	internal void Cancel()
	{
		if ( saving != null ) return;
		level.Audio?.ConfigurePreferences( originalPreferences ); level.Audio?.ConfigureQuality( LobbyOptionsSettings.Current.AudioQuality );
		Close();
	}
	private void Close() { Active = false; if ( level.Hud.Modal == this ) level.Hud.Modal = null; }
	private static PlayerProfileFile.Preference Preference( int id ) => id switch
	{
		0xc1 => PlayerProfileFile.Preference.Advisor, 0xc2 => PlayerProfileFile.Preference.Tutorial,
		0xc3 => PlayerProfileFile.Preference.Tooltips, 0xc4 => PlayerProfileFile.Preference.ConfirmDelete,
		0xc5 => PlayerProfileFile.Preference.RightMouseCancel, 0xc6 => PlayerProfileFile.Preference.Isometric,
		0xc7 => PlayerProfileFile.Preference.RightMouseScroll, _ => throw new ArgumentOutOfRangeException( nameof(id) )
	};
	private int Percent( int id ) => id switch
	{
		0xd5 => Math.Max( 0, draft.Resolution * 50 ), 0xd6 => draft.Graphics * 50, 0xe0 => draft.AudioQuality,
		_ => preferences.SoundVolume( (PlayerProfileFile.SoundChannel)((id - 0xd8) / 2) )
	};
	/// <summary>US 0x4a2e90 quantizes resolution/detail by truncation; volume and quality remain 0..100.</summary>
	internal void SetPercent( int id, int percent )
	{
		percent = Math.Clamp( percent, 0, 100 );
		if ( id == 0xd5 ) { draft.Resolution = percent * 2 / 100; status.Text = "Resolution changes require a restart."; }
		else if ( id == 0xd6 ) draft.Graphics = percent * 2 / 100;
		// US 0x4a2e90 updates the quality field only; 0x4237f0 changes the voice budget on OK.
		else if ( id == 0xe0 ) draft.AudioQuality = percent;
		else
		{
			preferences.SetSoundVolume( (PlayerProfileFile.SoundChannel)((id - 0xd8) / 2), percent );
			level.Audio?.ConfigurePreferences( preferences.Preferences );
		}
		Refresh();
	}
	internal void Activate( int id )
	{
		if ( id == -2 ) { Cancel(); return; }
		if ( id == -1 ) { Commit(); return; }
		if ( id is 0xd2 or 0xd3 ) { status.Text = "Rendering backend and video card are managed by OpenTPW."; return; }
		if ( id >= 0xc1 && id <= 0xc7 )
		{
			var key = Preference( id ); preferences.SetEnabled( key, !preferences.Enabled( key ) );
		}
		else if ( id is 0xd7 or 0xd9 or 0xdb or 0xdd )
		{
			var channel = (PlayerProfileFile.SoundChannel)((id - 0xd7) / 2);
			preferences.SetSoundEnabled( channel, !preferences.SoundEnabled( channel ) );
			level.Audio?.ConfigurePreferences( preferences.Preferences );
		}
		Refresh();
	}
	private void Commit()
	{
		draft.Preferences = (byte[])preferences.Preferences.Clone();
		var values = draft.Copy(); var bytes = (byte[])preferences.Preferences.Clone();
		string root = SaveFileSystem.GetAbsolutePath( "" );
		string? path = level.SelectedPlayer == null ? null : Path.Combine( level.SelectedPlayer.Directory, "gms.dat" );
		string settingsPath = Settings.Default.FilePath;
#if DEBUG
		settingsPath = PersistencePathOverride ?? settingsPath;
#endif
		saving = Task.Run( () => LobbyOptionsSettings.Save( root, values, path, bytes, settingsPath ) );
		status.Text = "Saving...";
	}
	private void Refresh()
	{
		string T( UIStrings key ) => Localization.Text( key );
		language.Text = "Language: " + (draft.Language.Length == 0 ? GameLanguage.Name : draft.Language);
		pack.Text = PackLabel( draft.LanguagePack );
		foreach ( var c in controls )
		{
			if ( IsToggle( c.Id ) ) c.Model.State = ToggleState( ToggleEnabled( c.Id ), false );
			if ( c.Id is >= 0xc8 and <= 0xd1 && c.Label != null )
			{
				UIStrings label = c.Id switch { 0xc8 => UIStrings.GPURendering, 0xca => UIStrings.Videocard,
					0xcb => UIStrings.Advisor, 0xcc => UIStrings.Tutorial, 0xcd => UIStrings.PopupHelp,
					0xce => UIStrings.Confirmations, 0xcf => UIStrings.RmbCancel,
					0xd0 => UIStrings.Rotation,
					_ => UIStrings.Scroll };
				c.Label.Text = T( label );
				if ( c.Id == 0xca ) c.Label.Text += T( UIStrings.Primary );
				if ( c.Id >= 0xcb && c.Id <= 0xcf )
					c.Label.Text += T( preferences.Enabled( Preference( 0xc1 + c.Id - 0xcb ) ) ? UIStrings.On : UIStrings.Off );
				if ( c.Id == 0xd0 ) c.Label.Text = T( UIStrings.Rotation ) + T( preferences.Enabled( PlayerProfileFile.Preference.Isometric ) ? UIStrings.Rotation90Degs : UIStrings.Smooth );
				if ( c.Id == 0xd1 ) c.Label.Text = T( UIStrings.Scroll ) + T( preferences.Enabled( PlayerProfileFile.Preference.RightMouseScroll ) ? UIStrings.RightButton : UIStrings.Pushscroll );
			}
			if ( c.Handle == null ) continue;
			// Table 0x752f30 gives a 258-unit track and a 67-unit handle: 191 units of travel.
			int percent = Percent( c.Id ); c.Handle.Bounds = Box( 990 + percent * 191 / 100f, c.HandleY, 1057 + percent * 191 / 100f, c.HandleY + 68 );
			UIStrings name; string value;
			if ( c.Id == 0xd5 ) { name = UIStrings.ScreenResolution; value = draft.Resolution < 0 ? T( UIStrings.Custom ) : T( new[] { UIStrings.Resolution512x384, UIStrings.Resolution640x480, UIStrings.Resolution800x600 }[draft.Resolution] ); }
			else if ( c.Id == 0xd6 ) { name = UIStrings.GraphicsQuality; value = T( new[] { UIStrings.Low, UIStrings.Medium, UIStrings.High }[draft.Graphics] ); }
			else if ( c.Id == 0xe0 ) { name = UIStrings.AudioQuality; value = percent + " %"; }
			else
			{
				int channel = (c.Id - 0xd8) / 2; name = new[] { UIStrings.SoundEffectsVolume, UIStrings.MusicVolume, UIStrings.SpeechVolume, UIStrings.MovieVolume }[channel];
				value = preferences.SoundEnabled( (PlayerProfileFile.SoundChannel)channel ) ? percent + " %" : T( UIStrings.Off );
			}
			c.Label!.Text = T( name ) + " " + value.Trim();
		}
	}
	private static bool IsToggle( int id ) => id is >= 0xc1 and <= 0xc5 or 0xd7 or 0xd9 or 0xdb or 0xdd;
	private bool ToggleEnabled( int id ) => id <= 0xc5 ? preferences.Enabled( Preference( id ) ) :
		preferences.SoundEnabled( (PlayerProfileFile.SoundChannel)((id - 0xd7) / 2) );
	/// <summary>
	/// US 0x4a3a30 passes !enabled to 0x668bec (+0x138). 0x668820 maps that OFF bit to
	/// roots 0/5 at rest and 2/3 while hovered. It is not the naming radio button's selected state.
	/// </summary>
	private static int ToggleState( bool enabled, bool hovered ) => hovered ? enabled ? 2 : 3 : enabled ? 0 : 5;
	protected override void OnUpdate()
	{
		if ( !Active || opened == Input.FrameNumber ) return;
		if ( saving != null )
		{
			if ( !saving.IsCompleted ) return;
			if ( saving.IsFaulted ) { Log.Warning( saving.Exception!.ToString() ); saving = null; status.Text = "Save failed. Retry OK or Cancel."; return; }
			saving = null; LobbyOptionsSettings.Accept( draft );
			// US 0x423740/0x4237f0 apply the selected detail preset on OK. Resolution/backend changes
			// instead display UITEXT 0x193 (RestartGame); they are not a timed display confirmation.
			LobbyQuality.Apply( new[] { "low", "med", "high" }[draft.Graphics] );
			level.Audio?.ConfigureQuality( draft.AudioQuality );
			Settings.Default.Language = draft.Language; Settings.Default.LanguagePack = draft.LanguagePack;
			if ( level.PlayerMenu?.Profile is { } profile ) preferences.Preferences.CopyTo( profile.Preferences, 0 );
			if ( level.Advisor != null ) level.Advisor.Enabled = preferences.Enabled( PlayerProfileFile.Preference.Advisor );
			Close(); return;
		}
		if ( Input.Pressed( Key.Escape ) ) { Cancel(); return; }
		if ( Input.LeftPressed && LobbyNamingPanel.Hit( Box( 1331, 20, 1964, 87 ) ) )
		{
			var available = packLanguages[draft.LanguagePack].Concat( baseLanguages ).Distinct( StringComparer.OrdinalIgnoreCase ).ToArray();
			int index = Array.FindIndex( available, value => value.Equals( draft.Language.Length == 0 ? GameLanguage.Name : draft.Language, StringComparison.OrdinalIgnoreCase ) );
			draft.Language = available[(index + 1) % available.Length]; status.Text = "Language changes require a restart."; Refresh(); return;
		}
		if ( Input.LeftPressed && LobbyNamingPanel.Hit( Box( 1331, 87, 1964, 154 ) ) )
		{
			int index = Array.FindIndex( packs, value => value.Equals( draft.LanguagePack, StringComparison.OrdinalIgnoreCase ) );
			draft.LanguagePack = packs[(index + 1) % packs.Length];
			var available = packLanguages[draft.LanguagePack];
			draft.Language = LobbyOptionsSettings.ChooseLanguage( available.Length == 0 ? baseLanguages : available,
				draft.Language.Length == 0 ? GameLanguage.Name : draft.Language );
			status.Text = "Language changes require a restart."; Refresh(); return;
		}
		if ( Input.Pressed( Key.Tab ) || Input.Pressed( Key.Down ) ) focused = (focused + 1) % interactive.Length;
		if ( Input.Pressed( Key.Up ) ) focused = (focused + interactive.Length - 1) % interactive.Length;
		for ( int i = 0; i < interactive.Length; i++ )
		{
			var c = interactive[i]; bool hit = LobbyNamingPanel.Hit( c.Bounds );
			if ( c.Handle == null )
			{
				c.Model.State = c.Id is 0xd2 or 0xd3 ? 1 : IsToggle( c.Id ) ? ToggleState( ToggleEnabled( c.Id ), hit ) :
					hit ? (Input.Mouse.Left ? 3 : 2) : 0;
			}
			// Toggle bounds overlap their audio slider's background. Only the track is draggable.
			if ( c.Handle != null ) hit &= Input.Mouse.Position.X * 2048 / Screen.Size.X >= 962;
			if ( !hit || !Input.LeftPressed ) continue;
			focused = i;
			if ( c.Handle != null ) dragging = c.Id;
			else { Activate( c.Id ); return; }
		}
		if ( dragging != -1 )
		{
			if ( Input.Mouse.Left ) SetPercent( dragging, (int)((Input.Mouse.Position.X * 2048 / Screen.Size.X - 1023) * 100 / 191) );
			else dragging = -1;
		}
		var selected = interactive[focused];
		if ( selected.Handle != null && (Input.Pressed( Key.Left ) || Input.Pressed( Key.Right )) )
			SetPercent( selected.Id, Percent( selected.Id ) + (Input.Pressed( Key.Right ) ? 1 : -1) * (selected.Id is 0xd5 or 0xd6 ? 50 : 1) );
		if ( Input.Pressed( Key.Enter ) || Input.Pressed( Key.Space ) ) Activate( selected.Handle == null ? selected.Id : -1 );
	}
	protected override void OnRender()
	{
		if ( !Active ) return;
		background.Draw();
		foreach ( var c in controls ) { c.Model.Draw(); c.Handle?.Draw(); c.Label?.Draw(); }
		title.Draw(); language.Draw(); pack.Draw(); status.Draw();
	}
}

