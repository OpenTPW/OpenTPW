using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class RotationTests
{
	[TestMethod]
	public void LookAtTheOppositeForwardAxisProducesAUnitHalfTurn()
	{
		var rotation = Rotation.LookAt( -Vector3.Forward );
		Assert.AreEqual( 1f, Vector3.Dot( rotation.Forward, -Vector3.Forward ), 0.00001 );
		Assert.AreEqual( 1f, Vector3.Dot( rotation.Up, Vector3.Up ), 0.00001 );
		Assert.AreEqual( 1f, Vector3.Dot( Rotation.LookAt( Vector3.Forward ).Forward, Vector3.Forward ), 0.00001 );
	}
}
