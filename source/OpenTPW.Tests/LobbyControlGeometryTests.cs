using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyControlGeometryTests
{
	[TestMethod]
	public void ConfirmationFontCoversTheTraditionalChineseQuitMessage()
	{
		GameData.Require();
		var text = new StringFile( GameLanguage.Path( "UITEXT.str" ) )[9];
		var font = new FontFile( GameLanguage.Path( $"{LobbyFonts.Name( 4 )}.bf4" ) );
		Assert.IsTrue( text.Contains( '\n' ) );
		foreach ( char character in text.Where( character => !char.IsWhiteSpace( character ) ) )
		{
			var glyph = font.GetGlyph( character );
			Assert.IsNotNull( glyph, $"Missing quit message glyph U+{(int)character:X4}" );
			Assert.IsTrue( glyph!.Advance > 0 && glyph.Pixels.Any( pixel => pixel > 0 ) );
		}
	}
	[TestMethod]
	public void DialogBackgroundUsesRegisteredRootNameRatherThanFileName()
	{
		GameData.Require();
		Assert.AreEqual( 0xcd93131eu, LobbyControlResources.NameHash( "wdialogw" ) );
		Assert.AreNotEqual( 0xcd93131eu, LobbyControlResources.NameHash( "w_dialog_wave" ) );
		Assert.AreEqual( 0xffffffd1u, LobbyControlResources.NameHash( "\u00ff" ) );
		string path = LobbyControlResources.ResolveModel( 0xcd93131e );
		Assert.IsTrue( path.EndsWith( "w_dialog_wave.MD2", StringComparison.OrdinalIgnoreCase ), path );
		var model = new LobbyButtonGeometry( FileSystem.ReadAllBytes( path ) );
		CollectionAssert.AreEqual( new[] { 0 }, model.VisibleMeshes );
		var wave = model.Model.Meshes[0].Materials.Single( material => material.Name == "wave" );
		Assert.AreEqual( 0x31u, wave.Flags );
		Assert.AreEqual( SamplerType.PointWrap, LobbyUiModel.TextureSampler( wave.Flags ) );
		Assert.AreEqual( SamplerType.PointWrap, LobbyUiModel.TextureSampler( 0x21 ) );
		Assert.AreEqual( SamplerType.Point, LobbyUiModel.TextureSampler( 1 ) );
		foreach ( var geometry in new[] { model, new LobbyButtonGeometry( FileSystem.ReadAllBytes( AnimationFiles.ResolveModel( "ui/LOLIGHT.md2" ) ) ) } )
		foreach ( var material in geometry.Model.Meshes.SelectMany( mesh => mesh.Materials ) )
		{
			string texture = AnimationFiles.ResolveFile( $"ui/textures/{material.Name}.wct", "Dialog texture" );
			Assert.IsTrue( new TextureFile( texture ).Data.Width > 0, texture );
		}
	}
	[TestMethod]
	public void ModelControlsSelectOneNativeStateAndFitItsOwnBounds()
	{
		GameData.Require();
		foreach ( string name in new[] { "b_okay", "b_exit", "b_ltoggle" } )
		{
			byte[] data = FileSystem.ReadAllBytes( AnimationFiles.ResolveModel( $"ui/{name}.md2" ) );
			for ( int state = 0; state < 6; state++ )
			{
				var geometry = new LobbyButtonGeometry( data, state );
				Assert.AreEqual( 6, geometry.StateRoots.Length );
				CollectionAssert.AreEqual( new[] { state }, geometry.VisibleMeshes );
				var bottomLeft = geometry.Project( geometry.Minimum, new Rectangle( 100, 100, 200, 50 ) );
				Assert.AreEqual( 100f / 320 - 1, bottomLeft.X, 0.000001 );
				Assert.AreEqual( 1 - 150f / 240, bottomLeft.Y, 0.000001 );
			}
			Assert.ThrowsException<ArgumentOutOfRangeException>( () => new LobbyButtonGeometry( data, 6 ) );
			int root = (int)BitConverter.ToUInt32( data, 0x78 );
			BitConverter.GetBytes( root ).CopyTo( data, root + 12 );
			Assert.ThrowsException<InvalidDataException>( () => new LobbyButtonGeometry( data ) );
		}
	}
}
