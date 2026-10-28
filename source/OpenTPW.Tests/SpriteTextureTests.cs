using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class SpriteTextureTests
{
	[TestMethod]
	public void ShippedParticleFramesDecodeIncludingTheKeySprite()
	{
		GameData.Require();
		var a = new SpriteTextureFile( FileSystem.ReadAllBytes( "esprites/Generic/Particles/SPR_PA.TPC" ) );
		var b = new SpriteTextureFile( FileSystem.ReadAllBytes( "esprites/Generic/Particles/SPR_PB.TPC" ) );
		Assert.AreEqual( 108, a.Frames.Count ); Assert.AreEqual( 16, b.Frames.Count );
		foreach ( var frame in a.Frames.Concat( b.Frames ) ) Assert.AreEqual( frame.Width * frame.Height, frame.Indices.Length );
		var key = a.Frames[101];
		var sequences = new SpriteSequenceFile( FileSystem.ReadAllBytes( "esprites/Generic/Particles/SPR_PA.ESP" ) );
		Assert.AreEqual( "SPR_PA.TPS", sequences.TextureName );
		Assert.AreEqual( 101, sequences.FrameIndex( 15, 0, 7, a.Frames.Count ) );
		foreach ( var sequence in sequences.Sequences.Select( (entry, index) => (entry, index) ) )
			for ( int frame = 0; frame < sequence.entry.FrameCount; frame++ )
				for ( int direction = 0; direction < 8; direction++ )
					Assert.IsTrue( sequences.FrameIndex( sequence.index, frame, direction, a.Frames.Count ) < a.Frames.Count );
		Assert.ThrowsException<ArgumentOutOfRangeException>( () => sequences.FrameIndex( 15, 1, 0, a.Frames.Count ) );
		Assert.ThrowsException<InvalidDataException>( () => sequences.FrameIndex( 15, 0, 0, 101 ) );
		Assert.AreEqual( 25, key.Width ); Assert.AreEqual( 31, key.Height );
		Assert.AreEqual( -13, key.OffsetX ); Assert.AreEqual( -15, key.OffsetY );
		CollectionAssert.AreEqual( Enumerable.Repeat( (byte)0, 13 ).Concat( new byte[] { 0x20, 0x6a, 0x5b, 0x47, 0x3f } )
			.Concat( Enumerable.Repeat( (byte)0, 7 ) ).ToArray(), key.Indices[..25] );
		Assert.IsTrue( key.Indices.Distinct().Count() > 30 );
	}

	[TestMethod]
	public void TpcRowsRejectTruncationAndOverruns()
	{
		byte[] Fixture( byte[] row )
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter( stream );
			writer.Write( (ushort)3 ); writer.Write( (ushort)3 ); writer.Write( 1 );
			for ( int i = 0; i < 256; i++ ) writer.Write( (uint)i );
			writer.Write( row.Length ); writer.Write( (ushort)4 ); writer.Write( (ushort)1 );
			writer.Write( (ushort)128 ); writer.Write( (ushort)128 ); writer.Write( -2 ); writer.Write( 0 );
			writer.Write( row );
			return stream.ToArray();
		}
		var valid = Fixture( new byte[] { 5, 2, 1, 2, 254, 3 } );
		CollectionAssert.AreEqual( new byte[] { 1, 2, 3, 3 }, new SpriteTextureFile( valid ).Frames[0].Indices );
		Assert.ThrowsException<InvalidDataException>( () => new SpriteTextureFile( valid[..^1] ) );
		Assert.ThrowsException<InvalidDataException>( () => new SpriteTextureFile( Fixture( new byte[] { 2, 251, 0 } ) ) );
		Assert.ThrowsException<InvalidDataException>( () => new SpriteTextureFile( Fixture( new byte[] { 2, 3, 0 } ) ) );
	}
}
