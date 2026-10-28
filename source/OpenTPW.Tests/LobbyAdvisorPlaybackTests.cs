using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyAdvisorPlaybackTests
{
	[TestMethod]
	public void LipBoundariesToggleOncePerUpdateAndDriveTimedMouthChoices()
	{
		byte[] bytes = new[] { 0, 200000, 500000, 600000, -1 }.SelectMany( BitConverter.GetBytes ).ToArray();
		var lips = new LobbyAdvisorLips( bytes );
		int draws = 0;
		int Next() { draws++; return 2; }
		lips.Update( -1, Next );
		Assert.AreEqual( 1, lips.Mouth );
		lips.Update( 1, Next );
		Assert.IsFalse( lips.Speaking );
		Assert.AreEqual( 0, draws );
		lips.Update( 201, Next );
		Assert.IsTrue( lips.Speaking );
		Assert.AreEqual( 3, lips.Mouth );
		lips.Update( 301, Next );
		Assert.AreEqual( 1, draws, "Retail uses a strict greater-than comparison" );
		lips.Update( 302, Next );
		Assert.AreEqual( 2, draws );
		lips.Update( 1000, Next );
		Assert.IsFalse( lips.Finished, "Only one boundary is consumed per update" );
		lips.Update( 1000, Next );
		Assert.IsTrue( lips.Finished );
		Assert.AreEqual( 1, lips.Mouth );
		Assert.ThrowsException<InvalidDataException>( () => new LobbyAdvisorLips( bytes[..^1] ) );
		Assert.ThrowsException<InvalidDataException>( () => new LobbyAdvisorLips( BitConverter.GetBytes( 100 ) ) );
		Assert.IsTrue( new LobbyAdvisorLips( BitConverter.GetBytes( -1 ) ).Finished );
	}

	[TestMethod]
	public void WelcomeQueueBindsNativeResponsesAndCanReplaceActiveSpeech()
	{
		GameData.Require();
		using var audio = new AudioSystem( 1, false );
		var advisor = new LobbyAdvisor( audio, () => 0 );
		advisor.Welcome( false );
		Assert.AreEqual( 2, advisor.PendingCount );
		advisor.Update( 0 );
		Assert.AreEqual( 0x186, advisor.Response );
		Assert.AreEqual( 0, audio.Mixer.ActiveVoices );
		advisor.Update( 0.799 );
		Assert.AreEqual( 0, audio.Mixer.ActiveVoices );
		advisor.Update( 0.8 );
		Assert.AreEqual( 1, audio.Mixer.ActiveVoices );
		advisor.Welcome( true );
		Assert.AreEqual( 0, audio.Mixer.ActiveVoices );
		Assert.AreEqual( 1, advisor.PendingCount );
		advisor.Update( 1 );
		Assert.AreEqual( 0x18e, advisor.Response );
		advisor.Cancel();
		Assert.IsFalse( advisor.Active );
		Assert.AreEqual( 0, advisor.PendingCount );
		advisor.Enabled = false;
		advisor.Welcome( false );
		Assert.AreEqual( 0, advisor.PendingCount );
		advisor.Enabled = true;
		advisor.Welcome( true );
		Assert.AreEqual( 1, advisor.PendingCount );
		advisor.Enabled = false;
		Assert.AreEqual( 0, advisor.PendingCount );
		foreach ( string name in new[] { "mouth - normal", "mouth - aah", "mouth - eee", "mouth - ooh", "mouth - sss" } )
			Assert.AreEqual( 1, advisor.Geometry.Model.Meshes.Count( mesh => mesh.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) ) );
	}

	[TestMethod]
	public void EveryImplementedLobbyResponseResolvesSpeechAndLipData()
	{
		GameData.Require();
		using var audio = new AudioSystem( 1, false );
		var advisor = new LobbyAdvisor( audio, () => 0 );
		foreach ( int response in new[] { 0x186, 0x187, 0x18e, 0x189, 0x18a, 0x23a, 0x250, 0x251, 0x252, 0x253, 0x254, 0x255, 0x256, 0x258 } )
		{
			advisor.Say( response, true );
			advisor.Update( 0 );
			Assert.AreEqual( response, advisor.Response );
			Assert.IsTrue( advisor.Active );
		}
		// 0x257 uses speech 638: present in US data, missing from the TW and CN speech maps.
		if ( audio.Assets.Category( "global/speech", "cat_speech" ).Sounds.ContainsKey( 638 ) )
		{
			advisor.Say( 0x257, true );
			advisor.Update( 0 );
			Assert.AreEqual( 0x257, advisor.Response );
		}
		else
		{
			var error = Assert.ThrowsException<InvalidDataException>( () => advisor.Say( 0x257 ) );
			StringAssert.Contains( error.Message, "missing speech 638" );
		}
	}

	[TestMethod]
	public void NewPlayerIntroductionKeepsTheNativeTimedCueAndCancelsItOnReplacement()
	{
		GameData.Require();
		using var audio = new AudioSystem( 1, false );
		var advisor = new LobbyAdvisor( audio, () => 0 );
		int cues = 0;
		advisor.ResponseCue += response => { Assert.AreEqual( 0x189, response ); cues++; };
		advisor.Say( 0x189 );
		advisor.Update( 0 );
		advisor.Update( 17.799 );
		Assert.AreEqual( 0, cues );
		advisor.Update( 17.8 );
		advisor.Update( 18 );
		Assert.AreEqual( 1, cues );
		advisor.Say( 0x189, true );
		advisor.Update( 20 );
		advisor.Say( 0x258, true );
		advisor.Update( 40 );
		advisor.Update( 58 );
		Assert.AreEqual( 1, cues );
	}

	[TestMethod]
	public void IntroductionCueResolvesItsActualCategoryAndProducesAudio()
	{
		GameData.Require();
		using var audio = new AudioSystem( 1, false );
		using var lobby = new LobbyAudio( audio );
		var advisor = new LobbyAdvisor( audio, () => 0 );
		long cue = 0;
		// Exercise the same timed response and production audio binding as Level.
		// A timer-only test cannot detect a sound ID bound to the wrong category.
		advisor.ResponseCue += response => cue = lobby.IntroductionKey();
		advisor.Say( 0x189 );
		advisor.Update( 0 );
		advisor.Update( 17.799 );
		Assert.AreEqual( 0L, cue );
		advisor.Update( 17.8 );
		Assert.IsTrue( audio.IsPlaying( cue ), "The actual TW UI cue must resolve and start" );
		advisor.Cancel();
		Assert.IsTrue( audio.IsPlaying( cue ), "The independent cue survives speech cancellation" );
		Assert.IsTrue( System.Threading.SpinWait.SpinUntil( () => !audio.Mixer.IsPending( cue ), 10000 ), "The cue decodes in the background" );
		var pcm = new float[44100];
		audio.Mixer.Read( pcm, 0, pcm.Length );
		Assert.IsTrue( pcm.Any( value => Math.Abs( value ) > 0.00001f ), "The cue must decode to audible PCM" );
	}

	[TestMethod]
	public void AdvisorOwnLightBrightensFacesTowardsTheViewer()
	{
		var ambient = new System.Numerics.Vector3( 0x44 / 255f );
		var node = System.Numerics.Matrix4x4.CreateScale( 0.01125f, 0.001f, 0.015f );
		// The light travels into the screen (+Z), so a normal pointing out of it (−Z) is fully lit.
		var dark = System.Numerics.Vector3.Zero;
		Assert.AreEqual( System.Numerics.Vector3.One, LobbyAdvisorGeometry.Colour( ambient, node, -System.Numerics.Vector3.UnitZ, System.Numerics.Vector3.UnitX, dark ) );
		Assert.AreEqual( ambient, LobbyAdvisorGeometry.Colour( ambient, node, System.Numerics.Vector3.UnitZ, System.Numerics.Vector3.UnitX, dark ) );
	}

	[TestMethod]
	public void LobbyCameraLightAlsoReachesFacesTurnedAwayFromTheAdvisorLight()
	{
		var ambient = new System.Numerics.Vector3( 0x44 / 255f );
		var diffuse = new System.Numerics.Vector3( 0xee / 255f * 0.5f );
		var node = System.Numerics.Matrix4x4.CreateScale( 0.01125f, 0.001f, 0.015f );
		// A face pointing into the screen gets nothing from the advisor light but all of a scene light travelling
		// out of the screen (−Z), even through the advisor's non-uniform root scale.
		var colour = LobbyAdvisorGeometry.Colour( ambient, node, System.Numerics.Vector3.UnitZ, -System.Numerics.Vector3.UnitZ, diffuse );
		Assert.AreEqual( ambient.X + diffuse.X, colour.X, 1e-5f );
		// A scene light behind the face adds nothing.
		Assert.AreEqual( ambient, LobbyAdvisorGeometry.Colour( ambient, node, System.Numerics.Vector3.UnitZ, System.Numerics.Vector3.UnitZ, diffuse ) );
	}
}
