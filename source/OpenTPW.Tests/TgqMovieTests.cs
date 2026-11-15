using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public sealed class TgqMovieTests
{
	[TestMethod]
	public void AllRetailMoviesDecodeWithExpectedMetadataAndFrames()
	{
		GameData.Require();
		var files = GameData.FindFiles( ".tgq" );
		Assert.IsTrue( files.Count >= 3 );

		var expectedFrames = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase )
		{
			["bf.tgq"] = 255,
			["bub.tgq"] = 1174,
			["buc.tgq"] = 1141,
		};

		foreach ( var virtualPath in files )
		{
			using var stream = FileSystem.OpenRead( virtualPath );
			var movie = TgqMovieFile.Read( stream );
			var name = Path.GetFileName( virtualPath );

			Assert.AreEqual( 320, movie.Width, virtualPath );
			Assert.AreEqual( 352, movie.Height, virtualPath );
			Assert.AreEqual( 30, movie.FrameRate, virtualPath );
			Assert.AreEqual( 22050, movie.SampleRate, virtualPath );
			Assert.AreEqual( 2, movie.Channels, virtualPath );
			if ( expectedFrames.TryGetValue( name, out int expected ) )
				Assert.AreEqual( expected, movie.FrameCount, virtualPath );
			else
				Assert.IsTrue( movie.FrameCount > 0, virtualPath );
			Assert.IsTrue( movie.AudioSamples.Length > 0 && movie.AudioSamples.Length % 2 == 0, virtualPath );

			var audio = movie.GetAudioFloat();
			Assert.AreEqual( movie.AudioSamples.Length, audio.Length, virtualPath );
			Assert.IsTrue( audio.All( float.IsFinite ), virtualPath );

			for ( int i = 0; i < movie.FrameCount; i++ )
			{
				var frame = movie.DecodeFrame( i );
				Assert.AreEqual( movie.Width, frame.Width, virtualPath );
				Assert.AreEqual( movie.Height, frame.Height, virtualPath );
			}

			var rgba = movie.DecodeFrame( 0 ).ToRgba();
			Assert.AreEqual( movie.Width * movie.Height * 4, rgba.Length, virtualPath );
			Assert.AreEqual( 255, rgba[3], virtualPath );
		}
	}

	[TestMethod]
	public void EmptyStreamThrowsInvalidData()
	{
		Assert.ThrowsExactly<InvalidDataException>( () => TgqMovieFile.Read( ReadOnlySpan<byte>.Empty ) );
	}
}
