using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using N = System.Numerics;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyLightingTests
{
	[TestMethod]
	public void IslandLightAimsFromBesideTheEyeAndTiltsDown()
	{
		// Retail axes: eye at height 20 looking along -Z at a target on the ground.
		var direction = LobbyLighting.Islands.Aim( new N.Vector3( 0, 20, 100 ), new N.Vector3( 0, 0, 0 ) );
		Assert.AreEqual( 1, direction.Length(), 1e-5 );
		// Flattened, then -0.3 in Y before renormalising (0x5d3eb0).
		Assert.AreEqual( -0.3f / MathF.Sqrt( 1.09f ), direction.Y, 1e-4 );
		Assert.IsTrue( direction.Z < -0.9f, "The light travels with the camera's view" );
	}

	[TestMethod]
	public void ColourIsAmbientPlusFacingDiffuseClampedToOne()
	{
		var lighting = LobbyLighting.Islands;
		var toward = lighting.Colour( new N.Vector3( 0, 0, -1 ), new N.Vector3( 0, 0, 1 ) );
		var away = lighting.Colour( new N.Vector3( 0, 0, -1 ), new N.Vector3( 0, 0, -1 ) );
		Assert.AreEqual( N.Vector3.One, toward );
		Assert.AreEqual( 0x44 / 255f, away.X, 1e-6 );
		Assert.AreEqual( away, lighting.Colour( new N.Vector3( 0, 0, -1 ), N.Vector3.Zero ) );
	}

	[TestMethod]
	public void NodeSpaceDirectionUndoesTheNodeRotation()
	{
		var node = N.Matrix4x4.CreateRotationY( MathF.PI / 2 );
		var local = LobbyLighting.LocalDirection( N.Vector3.UnitX, node, N.Quaternion.Identity );
		var back = N.Vector3.TransformNormal( local, node );
		Assert.IsTrue( N.Vector3.Distance( back, N.Vector3.UnitX ) < 1e-5 );
	}

	[TestMethod]
	public void LobbyModelsCarryAuthoredUnitNormals()
	{
		GameData.Require();
		var model = new ModelFile( AnimationFiles.ResolveModel( "lobby/terrain/Jun_isle.md2" ) );
		var normals = model.Meshes.Where( mesh => mesh.Vertices.Length > 0 ).SelectMany( mesh => mesh.Normals ).ToArray();
		Assert.IsTrue( normals.Length > 0 );
		Assert.IsTrue( normals.All( normal => MathF.Abs( normal.Length - 1 ) < 0.01f ) );
		Assert.IsTrue( model.Meshes.All( mesh => mesh.PrelitColours == null ) );
	}
}
