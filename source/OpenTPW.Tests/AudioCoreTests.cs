using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.Audio;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SharedMixer = OpenTPW.Audio.SoundMixer;

namespace OpenTPW.Tests;

/// <summary>Common decoder, cache and host-configurable offline scheduling against real TPW SDT data.</summary>
[TestClass]
[DoNotParallelize]
public class AudioCoreTests
{
	[TestMethod]
	public void EveryRetailSdtEntryDecodesAndMixesWithoutAnAudioDevice()
	{
		GameData.Require();
		var unique = new HashSet<string>(); int entries = 0;
		foreach ( var input in ReadBanks() )
		{
			string path = input.Path;
			using var bank = new SdtArchive( new MemoryStream( input.Data ) );
			foreach ( var file in bank.soundFiles )
			{
				entries++;
				if ( !unique.Add( Convert.ToHexString( SHA256.HashData( file.Data ) ) ) ) continue;
				var pcm = AudioDecoder.Decode( file, new( 44100, 2 ) );
				Assert.IsTrue( pcm.Length > 0 && pcm.Length % 2 == 0, path + ":" + file.Name );
				Assert.IsTrue( pcm.All( float.IsFinite ), path + ":" + file.Name );
				var mixer = new SharedMixer( new( 64 ) );
				long voice = mixer.Play( pcm ); var output = new float[Math.Min( 256, pcm.Length )];
				mixer.Read( output, 0, output.Length );
				CollectionAssert.AreEqual( pcm[..output.Length].Select( v => Math.Clamp( v, -1, 1 ) ).ToArray(), output );
				mixer.Stop( voice ); Assert.AreEqual( 0, mixer.ActiveVoices );
			}
		}
		Assert.AreEqual( 3741, entries, "All loose and WAD-contained SDT entries must participate." );
	}

	[TestMethod]
	public void ShortSilentMpegDecodesItsCompleteFramesAndReportsOnlyAlignment()
	{
		GameData.Require();
		using var bank = new SdtArchive( FileSystem.OpenRead( "global/sound/KidsHD.sdt" ) );
		var file = bank.soundFiles.First( f => Convert.ToHexString( SHA256.HashData( f.SoundData ) ) == "8A8E99FAFAC281C339F64963E3E40535DB201AB6C84460C99A3EBB248EC21F21" );
		var raw = MpegDecoder.Decode( file.SoundData );
		Assert.AreEqual( 1, raw.Layer ); Assert.AreEqual( new PcmFormat( 22050, 1 ), raw.Format );
		Assert.AreEqual( 1152, raw.Samples.Length ); Assert.AreEqual( 420, raw.ConsumedBytes ); Assert.AreEqual( 1, raw.TrailingBytes );
		Assert.IsTrue( raw.Samples.All( s => s == 0 ) );
		var pcm = AudioDecoder.Decode( file, new( 44100, 2 ) );
		Assert.AreEqual( 4608, pcm.Length ); Assert.IsTrue( pcm.All( s => s == 0 ) );
		// The last frame is complete: no following sync header is needed when length is bounded correctly.
		Assert.AreEqual( 1152, MpegDecoder.Decode( file.SoundData.AsSpan( 0, 420 ) ).Samples.Length );
		Assert.ThrowsExactly<InvalidDataException>( () => MpegDecoder.Decode( file.SoundData.AsSpan( 0, 100 ) ) );
		var broken = new MP2File( 40, "truncated", file.SoundData[..^20], 22050, 16, 36, 0, [] );
		Assert.ThrowsExactly<InvalidDataException>( () => AudioDecoder.Decode( broken, new( 44100, 2 ) ) );
	}

	[TestMethod]
	public void PackedPcmUsesDeclaredDepthAndMonoSampleRate()
	{
		byte[] data = [0, 128, 0, 0, 255, 127];
		var file = new MP2File( 40, "raw", data, 44100, 16, 2, 0, data );
		var pcm = AudioDecoder.Decode( file, new( 44100, 2 ) );
		CollectionAssert.AreEqual( new[] { -1f, -1f, 0f, 0f, 32767f / 32768, 32767f / 32768 }, pcm );
		Assert.AreEqual( 8, new MP2File( 40, "bad", data, 44100, 8, 2, 0, data ).BitsPerSample );
		Assert.ThrowsExactly<InvalidDataException>( () => AudioDecoder.Decode( new MP2File( 40, "bad", data, 44100, 8, 2, 0, data ), new( 44100, 2 ) ) );
		Assert.ThrowsExactly<InvalidDataException>( () => MpegDecoder.Decode( new byte[512] ) );
	}

	[TestMethod]
	public async Task ConcurrentCacheRequestsShareOneTaskAndPcmAcrossEqualResources()
	{
		byte[] data = [0, 0, 255, 127];
		var file = new MP2File( 40, "raw", data, 44100, 16, 2, 0, data );
		var copy = new MP2File( 40, "copy", data, 44100, 16, 2, 0, data );
		var cache = new AudioClipCache( new( 44100, 2 ) );
		var tasks = await Task.WhenAll( Enumerable.Range( 0, 16 ).Select( _ => Task.Run( () => cache.GetAsync( file ) ) ) );
		foreach ( var pcm in tasks ) Assert.AreSame( tasks[0], pcm );
		Assert.AreSame( tasks[0], await cache.GetAsync( copy ) );
		Assert.AreSame( cache.GetAsync( file ), cache.GetAsync( copy ) );
		var failure = new MP2File( 40, "invalid", [1, 2, 3], 22050, 16, 36, 0, [1, 2, 3] );
		await Assert.ThrowsExactlyAsync<InvalidDataException>( async () => await cache.GetAsync( failure ) );
	}

	[TestMethod]
	public void HostPoliciesChangeBudgetTieOrderAndVirtualProgress()
	{
		var oldest = new SharedMixer( new( 64, NewestFirst: false, AdvanceVirtualVoices: false ), 1000, 1 ) { VoiceLimit = 1 };
		long first = oldest.Play( [0.25f, 0.5f], priority: 7 ), second = oldest.Play( [0.75f, 1], priority: 7 );
		var output = new float[1]; oldest.Read( output, 0, 1 ); Assert.AreEqual( 0.25f, output[0] );
		Assert.AreEqual( 0, oldest.PositionSeconds( second ) ); oldest.Stop( first );
		oldest.Read( output, 0, 1 ); Assert.AreEqual( 0.75f, output[0] );
		oldest.VoiceLimit = 64; Assert.AreEqual( 64, oldest.VoiceLimit );
		Assert.ThrowsExactly<ArgumentOutOfRangeException>( () => oldest.VoiceLimit = 65 );
		var task = new TaskCompletionSource<float[]>(); long pending = oldest.Play( task.Task ); oldest.Stop( pending );
		task.SetResult( [1] ); Assert.IsFalse( oldest.IsPlaying( pending ) );
		var broken = oldest.Play( Task.FromException<float[]>( new InvalidDataException( "fixture" ) ) );
		Assert.IsTrue( SpinWait.SpinUntil( () => !oldest.IsPlaying( broken ), 3000 ) );
		Assert.ThrowsExactly<InvalidDataException>( oldest.ThrowIfFailed );
	}
	private static IEnumerable<(string Path, byte[] Data)> ReadBanks()
	{
		string data = Path.Combine( GameData.GamePath!, "data" );
		foreach ( string path in Directory.EnumerateFiles( data, "*", SearchOption.AllDirectories ) )
		{
			if ( Path.GetExtension( path ).Equals( ".sdt", StringComparison.OrdinalIgnoreCase ) )
				yield return (path, File.ReadAllBytes( path ));
			else if ( Path.GetExtension( path ).Equals( ".wad", StringComparison.OrdinalIgnoreCase ) )
			{
				using var wad = new WadArchive( path );
				foreach ( var entry in Walk( wad.Root, path + ":" ) ) yield return entry;
			}
		}
	}
	private static IEnumerable<(string Path, byte[] Data)> Walk( ArchiveDirectory directory, string prefix )
	{
		foreach ( var child in directory.Children )
		{
			if ( child is ArchiveDirectory nested )
				foreach ( var entry in Walk( nested, prefix + child.Name + "/" ) ) yield return entry;
			else if ( child is WadArchiveFile member && child.Name!.EndsWith( ".sdt", StringComparison.OrdinalIgnoreCase ) )
			{
				try { yield return (prefix + child.Name, member.GetData()); }
				finally { member.Free(); }
			}
		}
	}
}
