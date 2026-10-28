using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyAdvisorTests
{
	[TestMethod]
	public void ShippedAdvisorResolvesItsModelAnimationsMaterialsAndNativeScreenTransform()
	{
		GameData.Require();
		var geometry = new LobbyAdvisorGeometry();
		Assert.AreEqual( 25, geometry.Model.Meshes.Count );
		Assert.AreEqual( 0.01125f, geometry.RootTransform.M11, 0.00000001f );
		Assert.AreEqual( -0.001f, geometry.RootTransform.M23, 0.00000001f );
		Assert.AreEqual( 0.015f, geometry.RootTransform.M32, 0.00000001f );
		var origin = LobbyAdvisorGeometry.Project( System.Numerics.Vector3.Zero );
		Assert.AreEqual( 0.6f, origin.X );
		Assert.AreEqual( -0.6f, origin.Y );
		Assert.AreEqual( 0.2f, origin.Z );
		foreach ( var mesh in geometry.Model.Meshes )
		foreach ( var material in mesh.Materials )
		{
			if ( string.IsNullOrEmpty( material.Name ) ) continue;
			var image = new TextureFile( $"global/advisor/textures/{material.Name}.wct" );
			Assert.IsTrue( image.Data.Width > 0 );
		}
		var animations = AnimationFiles.Segments( LobbyAdvisorGeometry.ModelPath, AnimationSlot.M );
		Assert.AreEqual( 15, animations.Count );
		for ( int i = 0; i < animations.Count; i++ )
		{
			var animation = new ModelAnimation( animations[i] );
			var pose = geometry.Pose( animation, animation.EndFrame );
			Assert.AreEqual( 29, pose.WorldTransforms.Length );
			if ( i is 13 or 14 )
			{
				CollectionAssert.AreEqual( new ushort[] { 14, 16, 17, 18, 19, 20, 21, 22, 23, 24 }, animation.HiddenNodes );
				foreach ( ushort node in animation.HiddenNodes ) Assert.IsTrue( pose.Hidden[node] );
				var start = geometry.Pose( animation, animation.StartFrame );
				Assert.IsFalse( start.Hidden[13], "The right hand is visible independently of the accessory list" );
				Assert.IsFalse( start.Hidden[15], "The left hand is visible independently of the accessory list" );
				Assert.IsTrue( start.Hidden[3] );
				Assert.IsTrue( start.Hidden[4] );
			}
		}
	}
	[TestMethod]
	public void SegmentHidingBindsAuthoredIdsAndRejectsTruncatedTables()
	{
		GameData.Require();
		var geometry = new LobbyAdvisorGeometry();
		string path = AnimationFiles.Segments( LobbyAdvisorGeometry.ModelPath, AnimationSlot.M )[13];
		byte[] data = FileSystem.ReadAllBytes( path );
		var animation = new ModelAnimation( new MemoryStream( data ) );
		int node = geometry.Model.Nodes.FindIndex( candidate => candidate.Id == animation.HiddenNodes[0] );
		Assert.IsTrue( node >= 0 );
		geometry.Model.Nodes[node].Id = 65535;
		animation.HiddenNodes[0] = 65535;
		Assert.IsTrue( geometry.Pose( animation, animation.StartFrame ).Hidden[node] );
		Assert.ThrowsException<InvalidDataException>( () => animation.Evaluate( geometry.Model, animation.StartFrame, ModelAnimation.SegmentHiding.NodeIndex ) );
		geometry.Model.Nodes[node].Flags |= 0x80000000;
		Assert.IsFalse( geometry.Pose( animation, animation.StartFrame ).Hidden[node] );
		// The shared evaluator only enables the instance hiding mode when explicitly requested.
		animation.Evaluate( geometry.Model, animation.StartFrame );
		int block = (int)BitConverter.ToUInt32( data, 0x98 );
		BitConverter.GetBytes( (uint)(data.Length - 1) ).CopyTo( data, block + 0x38 );
		Assert.ThrowsException<InvalidDataException>( () => new ModelAnimation( new MemoryStream( data ) ) );
	}

}
