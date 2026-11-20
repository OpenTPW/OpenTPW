using Microsoft.VisualStudio.TestTools.UnitTesting;
using StbImageSharp;
using StbImageWriteSharp;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>Checks thumbnail sampling and PNG encoding.</summary>
[TestClass]
public class ThumbnailTests
{
	[TestMethod]
	public void ThumbnailAveragesRgbaBlocksAndWritesPng()
	{
		var pixels = new byte[32 * 32 * 4];
		for ( int y = 0; y < 32; y++ )
		for ( int x = 0; x < 32; x++ )
		{
			int i = (y * 32 + x) * 4;
			pixels[i] = (byte)x;
			pixels[i + 1] = (byte)y;
			pixels[i + 2] = 80;
			pixels[i + 3] = 255;
		}
		var thumbnail = Editor.ResizeThumbnail( pixels, 32, 32 );
		using var stream = new MemoryStream();
		new ImageWriter().WritePng( thumbnail, 16, 16, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream );
		stream.Position = 0;
		var decoded = ImageResult.FromStream( stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha );
		Assert.AreEqual( 16, decoded.Width );
		Assert.AreEqual( 16, decoded.Height );
		CollectionAssert.AreEqual( new byte[] { 0, 0, 80, 255 }, decoded.Data[..4] );
		CollectionAssert.AreEqual( new byte[] { 30, 30, 80, 255 }, decoded.Data[^4..] );
	}

	[TestMethod]
	public void SmallImageFillsThumbnailAndInvalidDataFails()
	{
		var thumbnail = Editor.ResizeThumbnail( new byte[] { 1, 2, 3, 4 }, 1, 1 );
		for ( int i = 0; i < thumbnail.Length; i += 4 )
			CollectionAssert.AreEqual( new byte[] { 1, 2, 3, 4 }, thumbnail[i..(i + 4)] );
		Assert.ThrowsExactly<System.ArgumentException>( () => Editor.ResizeThumbnail( new byte[3], 1, 1 ) );
	}

	[TestMethod]
	public async System.Threading.Tasks.Task RealTextureProducesCachedPng()
	{
		GameData.Require();
		string directory = Path.Combine( Path.GetTempPath(), "OpenTPW-thumbnail-" + System.Guid.NewGuid() );
		var previousCache = CacheFileSystem;
		try
		{
			CacheFileSystem = new BaseFileSystem( directory );
			var editor = (Editor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( Editor ) );
			string texture = FileSystem.GetAbsolutePath( "/levels/jungle/terrain/textures/jgr_bas1.wct" );
			string path = await editor.CacheThumbnailAsync( texture );
			using var stream = CacheFileSystem.OpenRead( path );
			var decoded = ImageResult.FromStream( stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha );
			Assert.AreEqual( 16, decoded.Width );
			Assert.AreEqual( 16, decoded.Height );
		}
		finally
		{
			CacheFileSystem = previousCache;
			if ( Directory.Exists( directory ) ) Directory.Delete( directory, true );
		}
	}
}

