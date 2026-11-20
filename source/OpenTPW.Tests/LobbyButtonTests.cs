using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Numerics;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyButtonTests
{
	[TestMethod]
	public void ShippedButtonModelFitsRetailControlAndResolvesEveryMaterial()
	{
		GameData.Require();
		var geometry = new LobbyButtonGeometry( FileSystem.ReadAllBytes( "ui/b_login.md2" ) );
		Assert.AreEqual( 1164f, geometry.Maximum.X - geometry.Minimum.X, 0.001f );
		Assert.AreEqual( 147f, geometry.Maximum.Y - geometry.Minimum.Y, 0.001f );
		var frame = new Rectangle( 884f / 3.2f, 46f / 3.2f, 1164f / 3.2f, 147f / 3.2f );
		var topRight = geometry.Project( new System.Numerics.Vector3( geometry.Maximum.X, geometry.Maximum.Y, 0 ), frame );
		var bottomLeft = geometry.Project( new System.Numerics.Vector3( geometry.Minimum.X, geometry.Minimum.Y, 0 ), frame );
		Assert.AreEqual( 1f, topRight.X, 0.00001f );
		Assert.AreEqual( 1 - 2 * 46f / 1536, topRight.Y, 0.00001f );
		Assert.AreEqual( 1 - 2 * 193f / 1536, bottomLeft.Y, 0.00001f );
		Assert.AreEqual( 2 * 884f / 2048 - 1, bottomLeft.X, 0.00001f );
		int triangles = 0;
		foreach ( var mesh in geometry.Model.Meshes )
		{
			foreach ( var material in mesh.Materials )
			{
				var texture = new TextureFile( $"ui/textures/{material.Name}.wct" );
				Assert.IsTrue( texture.Data.Width > 0 );
			}
			for ( int i = 0; i < mesh.Indices.Length; i += 3 )
			{
				uint index = mesh.Vertices[mesh.Indices[i]].TextureIndex;
				Assert.IsTrue( index < mesh.Materials.Length );
				Assert.AreEqual( index, mesh.Vertices[mesh.Indices[i + 1]].TextureIndex );
				Assert.AreEqual( index, mesh.Vertices[mesh.Indices[i + 2]].TextureIndex );
				triangles++;
			}
		}
		Assert.AreEqual( 6, triangles );
	}
}
