using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyPlayerProfilesTests
{
	[TestMethod]
	public void DirectoryPrefixesSelectTheirOriginalSlotsDespiteOrderAndGaps()
	{
		var slots = LobbyPlayerProfiles.FromDirectories( new[] { "users/4四号/", "users/2Second" } );
		Assert.AreEqual( 4, slots.Length );
		Assert.IsNull( slots[0] );
		Assert.AreEqual( "Second", slots[1]!.Name );
		Assert.AreEqual( 1, slots[1]!.Slot );
		Assert.IsNull( slots[2] );
		Assert.AreEqual( "四号", slots[3]!.Name );
		Assert.AreEqual( "users/4四号/", slots[3]!.Directory );
	}

	[TestMethod]
	public void InvalidDirectoriesDoNotOccupySlotsAndLaterDuplicateReplacesEarlier()
	{
		var slots = LobbyPlayerProfiles.FromDirectories( new[] { "users/0Zero", "users/5Five", "users/name", "users/1", "users/1Old", "users/1New" } );
		Assert.AreEqual( "New", slots[0]!.Name );
		Assert.IsNull( slots[1] );
		Assert.IsNull( slots[2] );
		Assert.IsNull( slots[3] );
	}
}
