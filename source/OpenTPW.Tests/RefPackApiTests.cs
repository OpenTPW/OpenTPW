using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>Checks the public shared decoder and its explicit malformed-input errors.</summary>
[TestClass]
public sealed class RefPackApiTests
{
	[TestMethod]
	public void DecodesLiteralAndStopPackets()
	{
		CollectionAssert.AreEqual( new byte[] { 1, 2, 3 }, RefPack.Decompress( [0x10, 0xfb, 0, 0, 3, 0xff, 1, 2, 3] ) );
		CollectionAssert.AreEqual( new byte[] { 1, 2, 3, 4 }, RefPack.Decompress( [0x10, 0xfb, 0, 0, 4, 0xe0, 1, 2, 3, 4, 0xfc] ) );
	}

	[TestMethod]
	public void RejectsBadHeadersTruncationAndWrongOutputLength()
	{
		Assert.ThrowsExactly<InvalidDataException>( () => RefPack.Decompress( [0x10, 0xfb] ) );
		Assert.ThrowsExactly<InvalidDataException>( () => RefPack.Decompress( [0, 0, 0, 0, 0, 0] ) );
		Assert.ThrowsExactly<InvalidDataException>( () => RefPack.Decompress( [0x10, 0xfb, 0, 0, 4, 0xff, 1, 2, 3] ) );
	}
}
