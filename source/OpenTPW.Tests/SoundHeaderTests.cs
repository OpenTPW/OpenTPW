using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class SoundHeaderTests
{
	[TestMethod]
	public void PackedRetailHeaderPreservesRateTypePcmSizeAndFirstPayloadBytes()
	{
		using var bytes = new MemoryStream();
		using ( var writer = new BinaryWriter( bytes, System.Text.Encoding.ASCII, true ) )
		{
			writer.Write( 40 ); writer.Write( 4 );
			writer.Write( System.Text.Encoding.ASCII.GetBytes( "SP_465.mp2".PadRight( 16, '\0' ) ) );
			writer.Write( (ushort)48000 ); writer.Write( (byte)16 ); writer.Write( (byte)36 );
			writer.Write( 0 ); writer.Write( 96000 ); writer.Write( 0 );
			writer.Write( new byte[] { 0xff, 0xf5, 0x62, 0xc0 } );
		}
		bytes.Position = 0;
		var file = new SoundFile( bytes );
		var sound = file.GetFile( new MemoryStream() );
		Assert.AreEqual( 48000, sound.SampleRate );
		Assert.AreEqual( 16, sound.BitsPerSample );
		Assert.AreEqual( MP2File.SoundTypes.MP2_MONO, sound.SoundType );
		Assert.AreEqual( 96000, sound.Samples );
		CollectionAssert.AreEqual( new byte[] { 0xff, 0xf5, 0x62, 0xc0 }, sound.SoundData );
	}

	[TestMethod]
	public void ShippedLobbyWelcomeSpeechKeepsItsMpegHeader()
	{
		GameData.Require();
		var file = new SoundFile( "global/speech/speechHD/SP_465.mp2" );
		var sound = file.GetFile( new MemoryStream() );
		Assert.AreEqual( 40, sound.Header );
		Assert.AreEqual( 22050, sound.SampleRate );
		Assert.AreEqual( 16, sound.BitsPerSample );
		Assert.AreEqual( MP2File.SoundTypes.MP2_MONO, sound.SoundType );
		Assert.AreEqual( 0xff, (int)sound.SoundData[0] );
		Assert.AreEqual( 0xf5, (int)sound.SoundData[1] );
		Assert.IsTrue( sound.Samples > 0 );
	}
}
