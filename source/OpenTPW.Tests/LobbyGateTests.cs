using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Numerics;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyGateTests
{
	[TestMethod]
	public void CancellingAnEntranceFinishesOpeningBeforeClosing()
	{
		var played = new System.Collections.Generic.List<int>();
		var gate = new LobbyGatePlayback( played.Add );
		gate.Update( false );
		Assert.AreEqual( 0, played.Count, "The default gate holds its base pose" );
		gate.Request( 0, false );
		gate.Request( 1, false );
		gate.Update( false );
		CollectionAssert.AreEqual( new[] { 0 }, played );
		gate.Update( true );
		gate.Update( true );
		CollectionAssert.AreEqual( new[] { 0, 1 }, played, "Closing starts once and its final pose is held" );
		gate.Request( 0, true );
		CollectionAssert.AreEqual( new[] { 0, 1, 0 }, played );
	}

	[TestMethod]
	public void EveryGateHasThreeAnimationsAndReversibleEntrancePoses()
	{
		GameData.Require();
		foreach ( var config in LobbyConfig.Load().Islands )
		{
			var model = new ModelFile( AnimationFiles.ResolveModel( config.GateModelPath ) );
			var segments = AnimationFiles.Segments( config.GateModelPath, AnimationSlot.M );
			Assert.AreEqual( 3, segments.Count, config.GateModelPath );
			Assert.AreEqual( 0, AnimationFiles.Segments( config.GateModelPath, AnimationSlot.C ).Count,
				"The instance constructor's optional slot C must not replace the gate's base pose" );
			var animations = segments.Select( file => new ModelAnimation( file ) ).ToArray();
			foreach ( var animation in animations )
			{
				Assert.IsTrue( animation.Length > 0 );
				for ( int sample = 0; sample <= 8; sample++ )
				{
					var pose = animation.Evaluate( model, animation.StartFrame + sample * animation.Length / 8f );
					Assert.IsFalse( pose.WorldTransforms.Any( m => !float.IsFinite( m.M11 ) || !float.IsFinite( m.M41 ) || !float.IsFinite( m.M42 ) || !float.IsFinite( m.M43 ) ) );
					Assert.IsFalse( pose.Positions.Any( vertices => vertices != null && vertices.Any(
						v => !float.IsFinite( v.X ) || !float.IsFinite( v.Y ) || !float.IsFinite( v.Z ) ) ) );
				}
			}
			var entering = animations[0];
			var leaving = animations[1];
			var closed = entering.Evaluate( model, entering.StartFrame );
			var opened = entering.Evaluate( model, entering.EndFrame );
			var leaveStart = leaving.Evaluate( model, leaving.StartFrame );
			var leaveEnd = leaving.Evaluate( model, leaving.EndFrame );
			float openDifference = Difference( closed, opened );
			float entranceJoin = Difference( opened, leaveStart );
			float closedJoin = Difference( closed, leaveEnd );
			Console.WriteLine( $"{config.GateModelPath}: frames={string.Join( ",", animations.Select( a => a.Length ) )}; open={openDifference}; entranceJoin={entranceJoin}; closedJoin={closedJoin}" );
			Assert.IsTrue( openDifference > 0.01f, $"{config.GateModelPath} does not animate its gate" );
			Assert.IsTrue( entranceJoin < openDifference * 0.1f, $"{config.GateModelPath}: M1 end must be close to M2 start" );
			Assert.IsTrue( closedJoin < openDifference * 0.1f, $"{config.GateModelPath}: M2 must return close to the closed pose" );
		}
	}

	private static float Difference( ModelPose a, ModelPose b )
	{
		float maximum = 0;
		for ( int i = 0; i < a.WorldTransforms.Length; i++ )
		{
			var x = a.WorldTransforms[i];
			var y = b.WorldTransforms[i];
			maximum = Math.Max( maximum, new[] {
				Math.Abs( x.M11-y.M11 ), Math.Abs( x.M12-y.M12 ), Math.Abs( x.M13-y.M13 ),
				Math.Abs( x.M21-y.M21 ), Math.Abs( x.M22-y.M22 ), Math.Abs( x.M23-y.M23 ),
				Math.Abs( x.M31-y.M31 ), Math.Abs( x.M32-y.M32 ), Math.Abs( x.M33-y.M33 ),
				Math.Abs( x.M41-y.M41 ), Math.Abs( x.M42-y.M42 ), Math.Abs( x.M43-y.M43 ) }.Max() );
			if ( a.Positions[i] is { } av && b.Positions[i] is { } bv )
				for ( int j = 0; j < av.Length; j++ )
					maximum = Math.Max( maximum, System.Numerics.Vector3.Distance( av[j].GetSystemVector3(), bv[j].GetSystemVector3() ) );
		}
		return maximum;
	}
}

