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
		Assert.ThrowsExactly<InvalidDataException>( () => new LobbyAdvisorLips( bytes[..^1] ) );
		Assert.ThrowsExactly<InvalidDataException>( () => new LobbyAdvisorLips( BitConverter.GetBytes( 100 ) ) );
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
			var error = Assert.ThrowsExactly<InvalidDataException>( () => advisor.Say( 0x257 ) );
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
	public void AdvisorLightIsInvertedThroughTheScaledNodeChainAndRenormalised()
	{
		// Row-vector convention, full inverse including the 0.001 flattening, then one normalisation (Render_NormalizeVector (US 0x5743a0)).
		var node = System.Numerics.Matrix4x4.CreateRotationX( 0.7f ) * System.Numerics.Matrix4x4.CreateRotationY( -1.1f )
			* System.Numerics.Matrix4x4.CreateScale( 0.01125f, 0.001f, 0.015f ) * System.Numerics.Matrix4x4.CreateRotationZ( 0.4f );
		Assert.IsTrue( System.Numerics.Matrix4x4.Invert( node, out var inverse ) );
		var expected = System.Numerics.Vector3.Normalize( System.Numerics.Vector3.TransformNormal( LobbyAdvisorGeometry.LightDirection, inverse ) );
		var light = LobbyAdvisorGeometry.LocalLight( node );
		Assert.AreEqual( 0, (expected - light).Length(), 1e-5f );
		// Not the inverse transpose: that would leave the tilted light direction tilted instead of snapping it to the flattened axis.
		var transposed = System.Numerics.Vector3.Normalize( System.Numerics.Vector3.TransformNormal( LobbyAdvisorGeometry.LightDirection, System.Numerics.Matrix4x4.Transpose( inverse ) ) );
		Assert.IsTrue( (transposed - light).Length() > 0.1f );
	}

	[TestMethod]
	public void ShippedRootFlatteningTurnsTheAdvisorLightIntoAHeadOnLightAndHandsStayRigid()
	{
		GameData.Require();
		var geometry = new LobbyAdvisorGeometry();
		// In the root's own frame the light runs along the flattened axis (local Y, which maps to -Z on screen),
		// although it was authored 2.7 degrees off the screen normal.
		var head = LobbyAdvisorGeometry.LocalLight( geometry.RootTransform );
		Assert.IsTrue( head.Y < -0.99999f, $"root-space light {head}" );
		Assert.IsTrue( System.Numerics.Matrix4x4.Invert( geometry.RootTransform, out var rootInverse ) );
		var animations = AnimationFiles.Segments( LobbyAdvisorGeometry.ModelPath, AnimationSlot.M ).Select( path => new ModelAnimation( path ) ).ToArray();
		int[] hands = [ geometry.Model.Meshes.FindIndex( mesh => mesh.Name == "Right hand" ), geometry.Model.Meshes.FindIndex( mesh => mesh.Name == "Left Hand" ) ];
		foreach ( var animation in animations )
		for ( float frame = animation.StartFrame; frame <= animation.EndFrame; frame += 5 )
		{
			var pose = geometry.Pose( animation, frame );
			foreach ( int node in hands )
			{
				// Above the root the hand chain is a pure rotation in every frame: no mirror (which would flip the
				// winding against the normals) and no scale or shear (which would bend the head-on light).
				var chain = pose.WorldTransforms[node] * rootInverse;
				var x = new System.Numerics.Vector3( chain.M11, chain.M12, chain.M13 );
				var y = new System.Numerics.Vector3( chain.M21, chain.M22, chain.M23 );
				var z = new System.Numerics.Vector3( chain.M31, chain.M32, chain.M33 );
				Assert.AreEqual( 1, System.Numerics.Vector3.Dot( System.Numerics.Vector3.Cross( x, y ), z ), 2e-3f, $"node {node} frame {frame}" );
				Assert.AreEqual( 1, x.Length(), 2e-3f ); Assert.AreEqual( 1, y.Length(), 2e-3f ); Assert.AreEqual( 1, z.Length(), 2e-3f );
				// So the node-space light is the same head-on direction, merely rotated with the hand.
				var light = System.Numerics.Vector3.TransformNormal( LobbyAdvisorGeometry.LocalLight( pose.WorldTransforms[node] ), chain );
				Assert.AreEqual( 0, (light - head).Length(), 2e-3f, $"node {node} frame {frame}" );
			}
		}
	}

	[TestMethod]
	public void AdvisorIsLitByItsOwnLightOnly()
	{
		var ambient = new System.Numerics.Vector3( 0x44 / 255f );
		// The shipped root: flattened along local Y, which the root rotation maps to -Z on screen.
		var node = System.Numerics.Matrix4x4.CreateScale( 0.01125f, 0.001f, 0.015f ) * System.Numerics.Matrix4x4.CreateRotationX( -MathF.PI / 2 );
		var light = LobbyAdvisorGeometry.LocalLight( node );
		// The light travels into the screen, so a normal pointing out of it (local +Y) is fully lit.
		Assert.AreEqual( System.Numerics.Vector3.One, LobbyAdvisorGeometry.Colour( ambient, light, System.Numerics.Vector3.UnitY ) );
		// A face turned away stays at ambient: the scene's mask-0x70 lights are skipped for the screen-space advisor
		// (ModelInstance_Submit (US 0x57a750) sets list flag 0x2000000), so nothing can light it from behind.
		Assert.AreEqual( ambient, LobbyAdvisorGeometry.Colour( ambient, light, -System.Numerics.Vector3.UnitY ) );
		var half = LobbyAdvisorGeometry.Colour( ambient, light, System.Numerics.Vector3.Normalize( new( 0, 1, 1 ) ) );
		Assert.AreEqual( ambient.X + MathF.Sqrt( 0.5f ), half.X, 2e-3f );
		Assert.AreEqual( 3, typeof(LobbyAdvisorGeometry).GetMethod( nameof(LobbyAdvisorGeometry.Colour) )!.GetParameters().Length, "Colour takes no scene light" );
	}

	[TestMethod]
	public void AdvisorDrawsWithDepthAndClockwiseFrontFaces()
	{
		Assert.IsFalse( LobbyAdvisorPanel.DrawFlags.HasFlag( MaterialFlags.DisableDepthTest ), "retail keeps ZENABLE for model primitives" );
		Assert.IsFalse( LobbyAdvisorPanel.DrawFlags.HasFlag( MaterialFlags.DisableDepthWrite ), "no advisor mesh carries mesh flag 2" );
		Assert.IsFalse( LobbyAdvisorPanel.DrawFlags.HasFlag( MaterialFlags.DisableCulling ) );
		Assert.AreEqual( SDL.SDL_GPUFrontFace.SDL_GPU_FRONTFACE_CLOCKWISE, new OpenTPW.Graphics.MeshRenderState().FrontFace );
		// Render_CullBackfaces (US 0x581320): clockwise on a y-down screen is kept. Project output is y-up, so "right, then down" is clockwise.
		var a = new Vector3( 0, 0, 0.2f ); var b = new Vector3( 1, 0, 0.2f ); var c = new Vector3( 0, -1, 0.2f );
		Assert.IsTrue( LobbyAdvisorGeometry.FacesViewer( a, b, c ) );
		Assert.IsFalse( LobbyAdvisorGeometry.FacesViewer( a, c, b ) );
		// The vertex shader must hand the projected depth to the rasteriser; a constant would disable the depth test in effect.
		string shader = File.ReadAllText( Content.GetPath( "shaders/ui-lit.hlsl" ) );
		StringAssert.Contains( shader, "float4( input.position, 1 )" );
	}

	[TestMethod]
	public void ShippedAdvisorWindingAgreesWithItsNormalsAndHandsKeepAuthoredNormals()
	{
		GameData.Require();
		var geometry = new LobbyAdvisorGeometry();
		var model = geometry.Model;
		var animations = AnimationFiles.Segments( LobbyAdvisorGeometry.ModelPath, AnimationSlot.M ).Select( path => new ModelAnimation( path ) ).ToArray();
		// M10 moves the fingers with packed vertex keys; the mesh still has exactly one authored normal per vertex to light with.
		var waving = geometry.Pose( animations[9], animations[9].StartFrame + 20 );
		foreach ( string hand in new[] { "Right hand", "Left Hand" } )
		{
			int index = model.Meshes.FindIndex( mesh => mesh.Name == hand );
			Assert.IsNotNull( waving.Positions[index], $"{hand} is vertex animated" );
			Assert.AreEqual( model.Meshes[index].Vertices.Length, model.Meshes[index].Normals.Length );
		}
		// In the rest pose, faces that the head-on light reaches are the ones the retail winding test keeps.
		var pose = geometry.Pose( animations[13], animations[13].StartFrame );
		int lit = 0, kept = 0;
		for ( int index = 0; index < model.Meshes.Count; index++ )
		{
			var mesh = model.Meshes[index];
			if ( pose.Hidden[index] || mesh.Name.StartsWith( "Mouth", StringComparison.OrdinalIgnoreCase ) ) continue;
			var light = LobbyAdvisorGeometry.LocalLight( pose.WorldTransforms[index] );
			for ( int i = 0; i < mesh.Indices.Length; i += 3 )
			{
				var points = new Vector3[3]; var normal = System.Numerics.Vector3.Zero;
				for ( int k = 0; k < 3; k++ )
				{
					int vertex = (int)mesh.Indices[i + k];
					var source = pose.Positions[index]?[vertex] ?? mesh.Vertices[vertex].Position;
					points[k] = LobbyAdvisorGeometry.Project( System.Numerics.Vector3.Transform( source.GetSystemVector3(), pose.WorldTransforms[index] ) );
					normal += mesh.Normals[vertex].GetSystemVector3();
				}
				if ( -System.Numerics.Vector3.Dot( light, System.Numerics.Vector3.Normalize( normal ) ) < 0.5f ) continue;
				lit++;
				if ( LobbyAdvisorGeometry.FacesViewer( points[0], points[1], points[2] ) ) kept++;
			}
		}
		Assert.IsTrue( lit > 100, $"lit faces {lit}" );
		Assert.IsTrue( kept >= lit * 0.97, $"{kept} of {lit} lit faces wind towards the viewer" );
	}

	[TestMethod]
	public void NamingResponseKeepsPlayingUntilItsOwnEndWhenNothingCancelsIt()
	{
		GameData.Require();
		using var audio = new AudioSystem( 1, false );
		var advisor = new LobbyAdvisor( audio, () => 0 );
		// NamingDialog_Open (US 0x4a6e40) queues 0x23a with the replace flag; cancelling the dialog calls nothing on the advisor.
		advisor.Say( 0x23a, true );
		advisor.Update( 0 );
		Assert.AreEqual( 0x23a, advisor.Response );
		int total = advisor.ResponseMilliseconds;
		// 800 ms lead-in, the gesture sequence covering the speech plus 500 ms, and the closing M15 segment.
		Assert.IsTrue( total > 800 + 500, $"response length {total}" );
		// One segment is entered per update, as in retail, so step through the response.
		for ( int time = 0; time < total - 50; time += 20 ) advisor.Update( time / 1000.0 );
		Assert.IsTrue( advisor.Active, "the advisor stays until its response ends" );
		Assert.AreEqual( 14, advisor.SegmentIndex, "it leaves with the closing segment M15" );
	}
}
