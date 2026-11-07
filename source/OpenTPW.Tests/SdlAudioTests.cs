using OpenTPW.Audio;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static SDL.SDL3;

namespace OpenTPW.Tests;

/// <summary>Native SDL3 conversion, callback ownership and unavailable-device behavior.</summary>
[TestClass]
[DoNotParallelize]
public class SdlAudioTests
{
	[TestMethod]
	public void CallbackFeedsWholeFramesAndRepeatedDisposalReleasesTheDevice()
	{
		string? previous = SDL_GetHint( SDL_HINT_AUDIO_DRIVER );
		try
		{
			Assert.IsTrue( SDL_SetHint( SDL_HINT_AUDIO_DRIVER, "dummy" ) );
			for ( int attempt = 0; attempt < 3; attempt++ )
			{
				var mixer = new SoundMixer();
				long voice = mixer.Play( new[] { 0.25f, -0.25f }, loop: true );
				using var output = new SdlAudioOutput( mixer );
				Assert.IsTrue( SpinWait.SpinUntil( () => output.SubmittedFrames >= 1024, 3000 ), "SDL must actually invoke the pull callback" );
				output.ThrowIfFailed();
				Assert.IsTrue( mixer.IsPlaying( voice ) );
				output.Dispose(); output.Dispose();
				long stopped = output.SubmittedFrames;
				Thread.Sleep( 30 );
				Assert.AreEqual( stopped, output.SubmittedFrames, "No callback can use the freed context after disposal" );
			}
		}
		finally { SDL_SetHint( SDL_HINT_AUDIO_DRIVER, previous ); }
	}

	[TestMethod]
	public void MissingAudioDriverFallsBackToSilentWithoutCreatingVoices()
	{
		GameData.Require();
		string? previous = SDL_GetHint( SDL_HINT_AUDIO_DRIVER );
		try
		{
			Assert.IsTrue( SDL_SetHint( SDL_HINT_AUDIO_DRIVER, "opentpw-no-such-driver" ) );
			using var audio = new AudioSystem( 1 );
			Assert.IsFalse( audio.Available ); Assert.IsFalse( string.IsNullOrWhiteSpace( audio.Failure ) );
			Assert.AreEqual( 0L, audio.Play( "global/sound", "cat_globallobbysfx", 1 ) );
			audio.Update();
			Assert.AreEqual( 0, audio.Mixer.ActiveVoices );
		}
		finally { SDL_SetHint( SDL_HINT_AUDIO_DRIVER, previous ); }
	}

	[TestMethod]
	public void WavPcmConversionKeepsSamplesChannelsAndResampledDuration()
	{
		short[] samples = { short.MinValue, -16384, 0, 16384, short.MaxValue };
		var pcm = SoundAssets.Decode( Wav( 44100, samples ) );
		Assert.AreEqual( samples.Length * 2, pcm.Length );
		for ( int i = 0; i < samples.Length; i++ )
		{
			Assert.AreEqual( samples[i] / 32768f, pcm[i * 2], 0.000001f );
			Assert.AreEqual( pcm[i * 2], pcm[i * 2 + 1] );
		}
		var source = Enumerable.Range( 0, 2205 ).Select( i => (short)(10000 * Math.Sin( i * 2 * Math.PI * 440 / 22050 )) ).ToArray();
		var converted = SoundAssets.Decode( Wav( 22050, source ) );
		Assert.AreEqual( 8820, converted.Length );
		for ( int i = 0; i < converted.Length; i += 2 )
		{
			Assert.IsTrue( float.IsFinite( converted[i] ) );
			Assert.AreEqual( converted[i], converted[i + 1] );
			// Ignore endpoint padding while checking interpolation against the known continuous signal.
			if ( i > 128 && i < converted.Length - 128 )
				Assert.AreEqual( 10000f / 32768 * (float)Math.Sin( i / 2 * 2 * Math.PI * 440 / 44100 ), converted[i], 0.001f );
		}
	}

	[TestMethod]
	public void InvalidWavAndPartialMixerFramesFailExplicitly()
	{
		var file = Wav( 44100, new short[] { 1, 2 } );
		file.SoundData = new byte[] { 1, 2, 3, 4 };
		Assert.ThrowsExactly<InvalidDataException>( () => SoundAssets.Decode( file ) );
		Assert.ThrowsExactly<ArgumentException>( () => new SoundMixer().Read( new float[3], 0, 3 ) );
		Assert.ThrowsExactly<ArgumentOutOfRangeException>( () => new SoundMixer().Read( new float[4], 2, 4 ) );
	}

	[TestMethod]
	public void EditorPreviewUsesTheHostAndReportsMissingAudioConnection()
	{
		var previous = ModKit.GlobalNamespace.PreviewSound;
		try
		{
			string? requested = null;
			ModKit.GlobalNamespace.PreviewSound = path => requested = path;
			var viewer = new ModKit.SoundViewer( "global/sound/preview.mp2" );
			viewer.PlaySound();
			Assert.AreEqual( "global/sound/preview.mp2", requested );
			ModKit.GlobalNamespace.PreviewSound = null;
			Assert.ThrowsExactly<InvalidOperationException>( viewer.PlaySound );
		}
		finally { ModKit.GlobalNamespace.PreviewSound = previous; }
	}
	private static MP2File Wav( int rate, short[] samples )
	{
		using var stream = new MemoryStream();
		using ( var writer = new BinaryWriter( stream, System.Text.Encoding.UTF8, true ) )
		{
			writer.Write( System.Text.Encoding.ASCII.GetBytes( "RIFF" ) ); writer.Write( 36 + samples.Length * 2 );
			writer.Write( System.Text.Encoding.ASCII.GetBytes( "WAVEfmt " ) ); writer.Write( 16 );
			writer.Write( (short)1 ); writer.Write( (short)1 ); writer.Write( rate ); writer.Write( rate * 2 );
			writer.Write( (short)2 ); writer.Write( (short)16 ); writer.Write( System.Text.Encoding.ASCII.GetBytes( "data" ) );
			writer.Write( samples.Length * 2 ); foreach ( short sample in samples ) writer.Write( sample );
		}
		var bytes = stream.ToArray();
		return new MP2File( 0, "test.wav", bytes, rate, 16, (int)MP2File.SoundTypes.WAV, samples.Length, bytes );
	}
}
