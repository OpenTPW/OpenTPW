using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class AudioTests
{
	[TestMethod]
	public void OutputBudgetRanksVoicesWithoutRestartingVirtualInstances()
	{
		var mixer = new SoundMixer( 1000, 2 ) { VoiceLimit = 1 };
		long low = mixer.Play( new[] { 0.1f, 0.1f, 0.2f, 0.2f, 0.3f, 0.3f }, priority: 1 );
		long high = mixer.Play( new[] { 0.6f, 0.6f }, delayMilliseconds: 1, priority: 2 );
		var output = new float[6];
		mixer.Read( output, 0, output.Length );
		CollectionAssert.AreEqual( new[] { 0.1f, 0.1f, 0.6f, 0.6f, 0.3f, 0.3f }, output );
		Assert.IsFalse( mixer.IsPlaying( low ) );
		Assert.IsFalse( mixer.IsPlaying( high ) );
		mixer.Play( new[] { 0.1f, 0.1f }, loop: true, priority: 7 );
		mixer.Play( new[] { 0.4f, 0.4f }, loop: true, priority: 7 );
		mixer.Read( output, 0, 2 );
		Assert.AreEqual( 0.4f, output[0], "Native active list visits the newest equal-priority instance first" );
		Assert.AreEqual( 2, mixer.ActiveVoices, "Output preemption must retain the logical instance" );
		mixer.VoiceLimit = 2;
		mixer.Read( output, 0, 2 );
		Assert.AreEqual( 0.5f, output[0], 0.00001f );
	}

	[TestMethod]
	public void SoundQualityUsesNativeIntervalsAndSchedulerCeiling()
	{
		GameData.Require();
		var settings = new SettingsFile( "sound.sam" );
		foreach ( var (quality, limit) in new[] { (0, 5), (25, 8), (50, 12), (70, 12), (71, 19), (100, 30) } )
			Assert.AreEqual( limit, SoundChannelPolicy.Limit( settings, quality ), $"Quality {quality}" );
		Assert.AreEqual( 12, SoundChannelPolicy.Limit( settings, 100, 12 ) );
		Assert.AreEqual( 30, SoundChannelPolicy.Limit( settings, 100, 64 ) );
		Assert.ThrowsException<ArgumentOutOfRangeException>( () => SoundChannelPolicy.Limit( settings, 101 ) );
	}

	[TestMethod]
	public void ProfileVolumeChangesUpdateAnExistingLoopWithoutRestartingIt()
	{
		GameData.Require();
		using var audio = new AudioSystem( 123, false );
		long handle = audio.Play( "levels/jungle/Sound", "cat_locallobbysfx", 2 );
		// First plays decode in the background.
		Assert.IsTrue( System.Threading.SpinWait.SpinUntil( () => !audio.Mixer.IsPending( handle ), 10000 ) );
		var output = new float[8820];
		audio.Mixer.Read( output, 0, output.Length );
		Assert.IsTrue( output.Any( value => value != 0 ) );
		double before = audio.Mixer.PositionSeconds( handle );
		byte[] preferences = PlayerProfileFile.DefaultPreferences( new SettingsFile( "sound.sam" ) );
		preferences[0] = 0;
		audio.ConfigurePreferences( preferences );
		audio.Mixer.Read( output, 0, output.Length );
		Assert.IsTrue( output.All( value => value == 0 ) );
		Assert.IsTrue( audio.Mixer.PositionSeconds( handle ) > before );
		Assert.IsTrue( audio.IsPlaying( handle ) );
		preferences[0] = 1;
		audio.ConfigurePreferences( preferences );
		audio.Mixer.Read( output, 0, output.Length );
		Assert.IsTrue( output.Any( value => value != 0 ) );
	}

	[TestMethod]
	public void ContinuationAvoidsNumericSampleIndexAcrossDefinitionsAndKeepsThresholdGaps()
	{
		var samples = new[] { new SoundCategoryMap.Sample( 10, 100, 10, 1, 0 ), new SoundCategoryMap.Sample( 20, 200, 10, 1, 0 ), new SoundCategoryMap.Sample( 30, 300, 10, 1, 0 ) };
		var definition = new SoundCategoryMap.Definition( 1, 100, 100, 0, 0, samples, Array.Empty<SoundCategoryMap.Relation>(), new byte[42] );
		Assert.AreEqual( 20u, SoundSelection.Sample( definition, 100, 0, true )!.Id );
		Assert.AreEqual( 10u, SoundSelection.Sample( definition, 250, 2, true )!.Id );
		Assert.AreEqual( 10u, SoundSelection.Sample( definition, 100, 0, false )!.Id );
		Assert.IsNull( SoundSelection.Sample( definition, 301, 2, true ) );
		var pair = definition with { Samples = samples[..2] };
		Assert.AreEqual( 10u, SoundSelection.Sample( pair, 100, 0, true )!.Id );
	}

	[TestMethod]
	public void ReusedNAudioByteBufferDoesNotReplayOrAccumulatePreviousSamples()
	{
		var mixer = new SoundMixer();
		var output = new NAudio.Wave.SampleProviders.SampleToWaveProvider( mixer );
		var bytes = new byte[16];
		mixer.Play( new[] { 0.1f, 0.2f, 0.3f, 0.4f } );
		Assert.AreEqual( 16, output.Read( bytes, 0, 16 ) );
		Assert.AreEqual( 0.4f, BitConverter.ToSingle( bytes, 12 ), 0.00001f );
		Assert.AreEqual( 16, output.Read( bytes, 0, 16 ) );
		CollectionAssert.AreEqual( new byte[16], bytes, "NAudio reuses its byte-backed float overlay after the voice ends" );
		mixer.Play( new[] { 0.05f, 0.06f, 0.07f, 0.08f }, loop: true );
		for ( int read = 0; read < 10; read++ )
		{
			output.Read( bytes, 0, bytes.Length );
			Assert.AreEqual( 0.08f, BitConverter.ToSingle( bytes, 12 ), 0.00001f, "Previous output must not be added to the next mix" );
		}
	}
	private static SoundCategoryMap Read( string directory, string category ) => SoundCategoryMap.Parse(
		FileSystem.ReadAllBytes( $"{directory}/{category}BANK.map" ), FileSystem.ReadAllBytes( $"{directory}/{category}SFX.map" ) );

	[TestMethod]
	public void PendingVoicePlaysOnlyAfterItsPcmArrives()
	{
		var mixer = new SoundMixer( 1000, 2 );
		var source = new System.Threading.Tasks.TaskCompletionSource<float[]>();
		long handle = mixer.Play( source.Task );
		var buffer = new float[4];
		mixer.Read( buffer, 0, buffer.Length );
		Assert.IsTrue( mixer.IsPlaying( handle ) );
		CollectionAssert.AreEqual( new float[4], buffer );
		source.SetResult( new[] { 0.5f, 0.25f } );
		System.Threading.SpinWait.SpinUntil( () => { mixer.Read( buffer, 0, 2 ); return buffer[0] != 0; }, 1000 );
		Assert.AreEqual( 0.5f, buffer[0] ); Assert.AreEqual( 0.25f, buffer[1] );
		mixer.Read( buffer, 0, 2 );
		Assert.IsFalse( mixer.IsPlaying( handle ) );
	}

	[TestMethod]
	public void FailedBackgroundDecodeIsReportedOnTheCallerThread()
	{
		var mixer = new SoundMixer( 1000, 2 );
		var source = new System.Threading.Tasks.TaskCompletionSource<float[]>();
		long handle = mixer.Play( source.Task );
		source.SetException( new InvalidDataException( "bad sample" ) );
		System.Threading.SpinWait.SpinUntil( () => !mixer.IsPlaying( handle ), 1000 );
		Assert.IsFalse( mixer.IsPlaying( handle ) );
		Assert.ThrowsException<InvalidDataException>( mixer.ThrowIfFailed );
		mixer.ThrowIfFailed();
	}

	[TestMethod]
	public void PcmIsSharedByFileAndByIdenticalPayload()
	{
		GameData.Require();
		var assets = new SoundAssets();
		var map = Read( "global/sound", "cat_globallobbysfx" );
		var file = assets.Sample( "global/sound", map, map.Sounds[1].Definitions[0].Samples[0] );
		var pcm = assets.Pcm( file );
		Assert.AreSame( pcm, assets.Pcm( file ) );
		var copy = new MP2File( file.Header, file.Name, file.SoundData, file.SampleRate, file.BitsPerSample, (int)file.SoundType, file.Samples, file.Data );
		Assert.AreSame( pcm, assets.Pcm( copy ) );
	}

	[TestMethod]
	public void LobbyBanksBindEveryCandidateAndDecodeActualMpeg()
	{
		GameData.Require();
		var assets = new SoundAssets();
		foreach ( string theme in new[] { "global", "jungle", "hallow", "fantasy", "space" } )
		{
			string directory = theme == "global" ? "global/sound" : $"levels/{theme}/Sound";
			var map = Read( directory, theme == "global" ? "cat_globallobbysfx" : "cat_locallobbysfx" );
			Assert.AreEqual( 4, map.Sounds.Count );
			foreach ( var sample in map.Sounds.Values.SelectMany( sound => sound.Definitions ).SelectMany( definition => definition.Samples ) )
			{
				var file = assets.Sample( directory, map, sample );
				Assert.IsFalse( string.IsNullOrWhiteSpace( file.Name ) );
				Assert.IsTrue( assets.Pcm( file ).Length >= 2, file.Name );
			}
		}
	}

	[TestMethod]
	public void NativeThresholdGapsAndDefinitionFallbackArePreserved()
	{
		GameData.Require();
		var map = Read( "global/sound", "cat_globallobbysfx" );
		var thunder = map.Sounds[1].Definitions[0];
		CollectionAssert.AreEqual( new uint[] { 2, 3, 4, 1 }, thunder.Samples.Select( sample => sample.Id ).ToArray() );
		Assert.AreEqual( 2u, SoundSelection.Sample( thunder, 16383 )!.Id );
		Assert.AreEqual( 3u, SoundSelection.Sample( thunder, 16384 )!.Id );
		Assert.IsNull( SoundSelection.Sample( thunder, 65535 ) );
		var space = map.Sounds[3];
		Assert.AreSame( space.Definitions[0], SoundSelection.Definition( space, 65535 ) );
		Assert.AreEqual( 80, new SoundSelection( 1 ).Range( thunder.VolumeMax, thunder.VolumeMin ) );
	}

	[TestMethod]
	public void IndependentHandlesMixOverlapStopLoopAndDelay()
	{
		var mixer = new SoundMixer( 1000, 2 );
		long first = mixer.Play( new[] { 0.2f, 0.2f }, 0.5f, true );
		long second = mixer.Play( new[] { 0.3f, 0.3f } );
		var data = new float[4];
		Assert.AreNotEqual( first, second );
		mixer.Read( data, 0, 4 );
		Assert.AreEqual( 0.4f, data[0], 0.00001f );
		Assert.AreEqual( 0.1f, data[2], 0.00001f );
		Assert.IsFalse( mixer.IsPlaying( second ) );
		Assert.IsTrue( mixer.IsPlaying( first ) );
		mixer.Stop( first );
		mixer.Play( new[] { 0.7f, 0.7f }, delayMilliseconds: 1 );
		mixer.Read( data, 0, 4 );
		Assert.AreEqual( 0f, data[0] );
		Assert.AreEqual( 0.7f, data[2] );
		Assert.AreEqual( 0, mixer.ActiveVoices );
	}

	[TestMethod]
	public void MapRejectsInvalidSignaturesCountsAndTruncation()
	{
		GameData.Require();
		byte[] bank = FileSystem.ReadAllBytes( "global/sound/cat_globallobbysfxBANK.map" );
		byte[] sfx = FileSystem.ReadAllBytes( "global/sound/cat_globallobbysfxSFX.map" );
		Assert.ThrowsException<InvalidDataException>( () => SoundCategoryMap.Parse( bank, sfx[..^1] ) );
		var bad = (byte[])sfx.Clone(); bad[0] ^= 0xff;
		Assert.ThrowsException<InvalidDataException>( () => SoundCategoryMap.Parse( bank, bad ) );
		bad = (byte[])sfx.Clone(); Array.Fill( bad, (byte)255, 24, 4 );
		Assert.ThrowsException<InvalidDataException>( () => SoundCategoryMap.Parse( bank, bad ) );
	}

	[TestMethod]
	public void LobbyPersistentHandlesSwitchThemeWithoutLeakingPreviousVoices()
	{
		GameData.Require();
		using var audio = new AudioSystem( 123, openDevice: false );
		using var lobby = new LobbyAudio( audio );
		foreach ( string theme in new[] { "jungle", "hallow", "fantasy", "space" } )
		{
			lobby.SetIsland( theme );
			Assert.AreEqual( 2, audio.Mixer.ActiveVoices, theme );
			lobby.SetIsland( theme );
			Assert.AreEqual( 2, audio.Mixer.ActiveVoices, theme );
		}
		lobby.Dispose();
		Assert.AreEqual( 0, audio.Mixer.ActiveVoices );
	}
}
