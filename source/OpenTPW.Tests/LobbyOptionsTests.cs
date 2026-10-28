using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>Options preserve the native profile layout and unrelated progress data.</summary>
[TestClass]
public class LobbyOptionsTests
{
	[TestMethod]
	public void NativeOptionsModelsAndTheirMaterialsExist()
	{
		GameData.Require();
		// Hashes come from US table 0x752f30, not model filenames inferred from the screenshot.
		foreach ( uint hash in new uint[] { 0x3aec1, 0x211ef0a0, 0xcea38960, 0x86cb3e31,
			0xcea389be, 0xcea389ed, 0x16ac3f4f, 0x14a7e48b, 0xbafbd0f8, 0xc8a6dfaf } )
		{
			string path = LobbyControlResources.ResolveModel( hash );
			Console.WriteLine( $"{hash:x8}: {path}" );
			var model = new ModelFile( path );
			Console.WriteLine( $"States: {new LobbyButtonGeometry( FileSystem.ReadAllBytes( path ) ).StateRoots.Length}" );
			Assert.IsTrue( model.Meshes.Any( mesh => mesh.Vertices.Length != 0 ), path );
			foreach ( string name in model.Meshes.SelectMany( mesh => mesh.Materials ).Select( material => material.Name.TrimEnd( '\0' ) ).Distinct() )
			{
				var texture = new TextureFile( AnimationFiles.ResolveFile( $"ui/textures/{name}.wct", "Option material" ) ).Data;
				Assert.IsTrue( texture.Width > 0 && texture.Height > 0, name );
			}
		}
	}
	[TestMethod]
	public void UsTableInsertionDoesNotShiftSemanticOptionsOrNamingLabels()
	{
		Assert.AreEqual( 6, UiStringLayout.Index( UIStrings.Options, 474 ) );
		Assert.AreEqual( 206, UiStringLayout.Index( UIStrings.NewSave, 474 ) );
		Assert.AreEqual( 208, UiStringLayout.Index( UIStrings.SendPostcard, 474 ) );
		Assert.AreEqual( 315, UiStringLayout.Index( UIStrings.GameOptions, 474 ) );
		Assert.AreEqual( 314, UiStringLayout.Index( UIStrings.GameOptions, 473 ) );
		Assert.AreEqual( 239, UiStringLayout.Index( UIStrings.EnterName, 474 ) );
		Assert.AreEqual( 242, UiStringLayout.Index( UIStrings.FullSimulation, 474 ) );
		Assert.ThrowsException<InvalidDataException>( () => UiStringLayout.Index( UIStrings.Options, 475 ) );
	}
	[TestMethod]
	public void PreferenceEditsPreservePaddingAndSurviveProfileSerialization()
	{
		var bytes = Enumerable.Range( 0, 39 ).Select( value => (byte)(value + 1) ).ToArray();
		var profile = PlayerProfileFile.Create( 1, bytes );
		var prefix = (byte[])profile.Prefix.Clone();
		foreach ( PlayerProfileFile.SoundChannel channel in Enum.GetValues<PlayerProfileFile.SoundChannel>() )
		{
			int offset = (int)channel * 8;
			profile.SetSoundEnabled( channel, false );
			profile.SetSoundVolume( channel, 37 + (int)channel );
			// Bytes 1..3 of each enabled field are undefined native padding, not option storage.
			CollectionAssert.AreEqual( bytes.Skip( offset + 1 ).Take( 3 ).ToArray(), profile.Preferences.Skip( offset + 1 ).Take( 3 ).ToArray() );
		}
		foreach ( PlayerProfileFile.Preference preference in Enum.GetValues<PlayerProfileFile.Preference>() )
			profile.SetEnabled( preference, (int)preference % 2 == 0 );
		var restored = PlayerProfileFile.Read( profile.Write() );
		CollectionAssert.AreEqual( prefix, restored.Prefix );
		Assert.AreEqual( (byte)1, restored.CreationMode );
		foreach ( PlayerProfileFile.SoundChannel channel in Enum.GetValues<PlayerProfileFile.SoundChannel>() )
		{
			Assert.IsFalse( restored.SoundEnabled( channel ) );
			Assert.AreEqual( 37 + (int)channel, restored.SoundVolume( channel ) );
		}
		foreach ( PlayerProfileFile.Preference preference in Enum.GetValues<PlayerProfileFile.Preference>() )
			Assert.AreEqual( (int)preference % 2 == 0, restored.Enabled( preference ) );
	}
	[TestMethod]
	public void InvalidOptionValuesCannotOverwriteAdjacentProfileBytes()
	{
		var profile = PlayerProfileFile.Create( 0, new byte[39] );
		var before = profile.Write();
		Assert.ThrowsException<ArgumentOutOfRangeException>( () => profile.SetSoundVolume( PlayerProfileFile.SoundChannel.Music, -1 ) );
		Assert.ThrowsException<ArgumentOutOfRangeException>( () => profile.SetSoundVolume( PlayerProfileFile.SoundChannel.Music, 101 ) );
		Assert.ThrowsException<ArgumentOutOfRangeException>( () => profile.SetSoundEnabled( (PlayerProfileFile.SoundChannel)4, true ) );
		Assert.ThrowsException<ArgumentOutOfRangeException>( () => profile.SetEnabled( (PlayerProfileFile.Preference)(-1), true ) );
		CollectionAssert.AreEqual( before, profile.Write() );
		profile.Preferences[4] = 101;
		Assert.ThrowsException<InvalidDataException>( () => profile.SoundVolume( PlayerProfileFile.SoundChannel.Effects ) );
	}
	[TestMethod]
	public void GlobalAndPlayerOptionsRoundTripWithoutChangingProgress()
	{
		string root = Path.Combine( Path.GetTempPath(), "OpenTPW-options-" + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( Path.Combine( root, "users", "1Options" ) );
		try
		{
			var profile = PlayerProfileFile.Create( 1, new byte[39] );
			profile.Prefix[0] = 79;
			string path = Path.Combine( root, "users", "1Options", "gms.dat" );
			File.WriteAllBytes( path, profile.Write() );
			var preferences = PlayerProfileFile.Create( 0, new byte[39] );
			preferences.SetSoundVolume( PlayerProfileFile.SoundChannel.Music, 37 );
			preferences.SetEnabled( PlayerProfileFile.Preference.RightMouseCancel, true );
			var settings = new LobbyOptionsSettings { Graphics = 2, AudioQuality = 64, Resolution = 2, Language = "american", Preferences = preferences.Preferences };
			string settingsPath = Path.Combine( root, Settings.FileName );
			var existing = Settings.Load( settingsPath ); existing.GamePath = "unchanged-game-dir"; existing.Save();
			LobbyOptionsSettings.Save( root, settings, path, preferences.Preferences, settingsPath );
			var restored = PlayerProfileFile.Read( File.ReadAllBytes( path ) );
			Assert.AreEqual( (byte)79, restored.Prefix[0] ); Assert.AreEqual( (byte)1, restored.CreationMode );
			Assert.AreEqual( 37, restored.SoundVolume( PlayerProfileFile.SoundChannel.Music ) );
			Assert.IsTrue( restored.Enabled( PlayerProfileFile.Preference.RightMouseCancel ) );
			var global = Settings.Load( Path.Combine( root, Settings.FileName ) );
			Assert.AreEqual( "high", global.LobbyQuality ); Assert.AreEqual( 64, global.AudioQuality );
			Assert.AreEqual( "unchanged-game-dir", global.GamePath );
			Assert.AreEqual( "american", global.Language ); CollectionAssert.AreEqual( preferences.Preferences, global.LobbyPreferences );
			Assert.IsFalse( Directory.EnumerateFiles( root, "*.tmp", SearchOption.AllDirectories ).Any() );
		}
		finally { Directory.Delete( root, true ); }
	}
	[TestMethod]
	public void FailedProfilePublishRestoresGlobalOptionsAndLeavesNoStagingFiles()
	{
		if ( !OperatingSystem.IsWindows() ) Assert.Inconclusive( "Windows file replacement sharing semantics" );
		string root = Path.Combine( Path.GetTempPath(), "OpenTPW-options-" + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( Path.Combine( root, "users", "1Options" ) );
		try
		{
			string global = Path.Combine( root, Settings.FileName );
			byte[] original = System.Text.Encoding.UTF8.GetBytes( "{\"GamePath\":\"keep-existing\"}" ); File.WriteAllBytes( global, original );
			string path = Path.Combine( root, "users", "1Options", "gms.dat" );
			byte[] profile = PlayerProfileFile.Create( 0, new byte[39] ).Write(); File.WriteAllBytes( path, profile );
			// Reading succeeds but replacement is denied while the destination is open without delete sharing.
			using ( var locked = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.Read ) )
				Assert.ThrowsException<UnauthorizedAccessException>( () => LobbyOptionsSettings.Save( root, new(), path, new byte[39], global ) );
			CollectionAssert.AreEqual( original, File.ReadAllBytes( global ) );
			CollectionAssert.AreEqual( profile, File.ReadAllBytes( path ) );
			Assert.IsFalse( Directory.EnumerateFiles( root, "*.tmp", SearchOption.AllDirectories ).Any() );
		}
		finally { Directory.Delete( root, true ); }
	}
	[TestMethod]
	public void RejectedSettingsCannotModifyAnotherPlayerOrGlobalFile()
	{
		string root = Path.Combine( Path.GetTempPath(), "OpenTPW-options-" + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( root );
		try
		{
			Assert.ThrowsException<InvalidDataException>( () => LobbyOptionsSettings.Save( root, new(), Path.Combine( root, "gms.dat" ), new byte[39], Path.Combine( root, Settings.FileName ) ) );
			Assert.ThrowsException<InvalidDataException>( () => LobbyOptionsSettings.Save( root, new() { Resolution = 3 }, null, new byte[39], Path.Combine( root, Settings.FileName ) ) );
			Assert.IsFalse( Directory.EnumerateFiles( root ).Any() );
		}
		finally { Directory.Delete( root, true ); }
	}
	[TestMethod]
	public void ApplyingQualityUpdatesExistingConsumersAndKeepsPresetsImmutable()
	{
		GameData.Require(); string previous = Settings.Default.LobbyQuality;
		try
		{
			var live = LobbyQuality.Load(); var low = LobbyQuality.Load( "low" );
			LobbyQuality.Apply( "high" ); Assert.AreSame( live, LobbyQuality.Load() );
			Assert.AreEqual( 100, live.ObjectsPercent ); Assert.IsFalse( live.UseSmallTextures );
			LobbyQuality.Apply( "low" ); Assert.AreSame( live, LobbyQuality.Load() );
			Assert.AreEqual( 10, live.ObjectsPercent ); Assert.IsTrue( live.UseSmallTextures );
			Assert.AreEqual( 10, low.ObjectsPercent ); Assert.AreEqual( "low", low.Preset );
		}
		finally { LobbyQuality.Apply( previous ); }
	}

	[TestMethod]
	public void StartupRestoresSavedGlobalValuesBeforeSubsystemConstruction()
	{
		string root = Path.Combine( Path.GetTempPath(), "OpenTPW-options-" + Guid.NewGuid().ToString( "N" ) );
		var previous = LobbyOptionsSettings.Current.Copy();
		string language = Settings.Default.Language, pack = Settings.Default.LanguagePack, quality = Settings.Default.LobbyQuality;
		var window = Settings.Default.GameWindowSize;
		int? audio = Settings.Default.AudioQuality; byte[]? defaults = Settings.Default.LobbyPreferences;
		try
		{
			var settings = new LobbyOptionsSettings { Resolution = 2, Graphics = 0, AudioQuality = 41, Language = "TraditionalChinese" };
			LobbyOptionsSettings.Save( Path.Combine( root, "save" ), settings, null, new byte[39], Path.Combine( root, Settings.FileName ) );
			var configuration = Settings.Load( Path.Combine( root, Settings.FileName ) );
			LobbyOptionsSettings.Initialize( configuration );
			Assert.AreEqual( "TraditionalChinese", configuration.Language );
			Assert.AreEqual( "low", configuration.LobbyQuality );
			Assert.AreEqual( new System.Drawing.Point( 800, 600 ), configuration.GameWindowSize );
			Assert.AreEqual( 41, LobbyOptionsSettings.Current.AudioQuality );
		}
		finally
		{
			LobbyOptionsSettings.Accept( previous );
			Settings.Default.Language = language; Settings.Default.LanguagePack = pack; Settings.Default.LobbyQuality = quality;
			Settings.Default.GameWindowSize = window; Settings.Default.AudioQuality = audio; Settings.Default.LobbyPreferences = defaults;
			if ( Directory.Exists( root ) ) Directory.Delete( root, true );
		}
	}

	[TestMethod]
	public void FirstStartupUsesShippedAudioQualityAndIgnoresWhitespacePackSetting()
	{
		GameData.Require();
		string root = Path.Combine( Path.GetTempPath(), "OpenTPW-options-" + Guid.NewGuid().ToString( "N" ) );
		var previous = LobbyOptionsSettings.Current.Copy();
		string language = Settings.Default.Language, pack = Settings.Default.LanguagePack, quality = Settings.Default.LobbyQuality;
		var window = Settings.Default.GameWindowSize;
		int? audio = Settings.Default.AudioQuality; byte[]? defaults = Settings.Default.LobbyPreferences;
		try
		{
			var configuration = Settings.Load( Path.Combine( root, Settings.FileName ) );
			configuration.LanguagePack = " \r\n "; configuration.Language = " ";
			LobbyOptionsSettings.Initialize( configuration );
			int expected = int.Parse( new SettingsFile( "sound.sam" )["SoundInfo.DEFAULTQUALITY"] );
			Assert.AreEqual( expected, LobbyOptionsSettings.Current.AudioQuality );
			Assert.AreEqual( "", LobbyOptionsSettings.Current.LanguagePack );
			Assert.AreEqual( "", LobbyOptionsSettings.Current.Language );
		}
		finally
		{
			LobbyOptionsSettings.Accept( previous ); Settings.Default.Language = language; Settings.Default.LanguagePack = pack;
			Settings.Default.LobbyQuality = quality; Settings.Default.GameWindowSize = window;
			Settings.Default.AudioQuality = audio; Settings.Default.LobbyPreferences = defaults;
		}
	}

	[TestMethod]
	public void PackSelectionHandlesSeveralFoldersAndRemovingAChineseOverlay()
	{
		Assert.AreEqual( "american", LobbyOptionsSettings.ChooseLanguage( new[] { "English", "american" }, "TraditionalChinese" ) );
		Assert.AreEqual( "English", LobbyOptionsSettings.ChooseLanguage( new[] { "English", "american" }, "ENGLISH" ) );
		Assert.AreEqual( "TraditionalChinese", LobbyOptionsSettings.ChooseLanguage( new[] { "TraditionalChinese" }, "american" ) );
		Assert.AreEqual( "French", LobbyOptionsSettings.ChooseLanguage( new[] { "French", "German" }, "american" ) );
		Assert.ThrowsException<InvalidDataException>( () => LobbyOptionsSettings.ChooseLanguage( Array.Empty<string>(), "american" ) );
	}

	[TestMethod]
	public void AudioPreviewSnapshotRetainsLivePreferencesAndRejectsPartialUpdates()
	{
		GameData.Require(); using var audio = new AudioSystem( 1, openDevice: false );
		var profile = PlayerProfileFile.Create( 0, audio.SnapshotPreferences() );
		profile.SetSoundVolume( PlayerProfileFile.SoundChannel.Music, 37 );
		audio.ConfigurePreferences( profile.Preferences );
		byte[] expected = audio.SnapshotPreferences();
		profile.Preferences[12] = 99;
		CollectionAssert.AreEqual( expected, audio.SnapshotPreferences() );
		var invalid = (byte[])expected.Clone(); invalid[4] = 12; invalid[28] = 101;
		Assert.ThrowsException<InvalidDataException>( () => audio.ConfigurePreferences( invalid ) );
		CollectionAssert.AreEqual( expected, audio.SnapshotPreferences() );
	}

}

