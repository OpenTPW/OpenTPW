using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class GameLanguageTests
{
	[TestMethod]
	public void RequestedFolderKeepsItsSpellingOnDisk()
	{
		Assert.AreEqual( "TraditionalChinese", GameLanguage.Resolve( new[] { "English", "TraditionalChinese" }, "traditionalchinese" ) );
	}

	[TestMethod]
	public void WithoutRequestEnglishWinsThenTheOnlyFolder()
	{
		Assert.AreEqual( "English", GameLanguage.Resolve( new[] { "TraditionalChinese", "English" }, "" ) );
		Assert.AreEqual( "TraditionalChinese", GameLanguage.Resolve( new[] { "TraditionalChinese" }, null ) );
	}

	[TestMethod]
	public void UsInstallsUseAmericanAndPacksChooseTheirOwnLanguage()
	{
		Assert.AreEqual( "american", GameLanguage.Resolve( new[] { "English", "american" }, null ) );
		Assert.AreEqual( "German", GameLanguage.Resolve( new[] { "English", "american", "German" }, null, new[] { "German" } ) );
		Assert.AreEqual( "english", GameLanguage.Resolve( new[] { "english", "American" }, "English", new[] { "German" } ) );
		Assert.ThrowsException<DirectoryNotFoundException>( () => GameLanguage.Resolve( new[] { "French", "German" }, null, new[] { "French", "German" } ) );
	}

	[TestMethod]
	public void Version2TablesShiftStringsFrom207()
	{
		Assert.AreEqual( 14, Localization.FileIndex( UIStrings.ConfirmNewPlayer, 474 ) );
		Assert.AreEqual( 421, Localization.FileIndex( UIStrings.CreateNewPlayer, 474 ) );
		Assert.AreEqual( 420, Localization.FileIndex( UIStrings.CreateNewPlayer, 473 ) );
	}

	[TestMethod]
	public void MissingOrAmbiguousLanguageFails()
	{
		Assert.ThrowsException<DirectoryNotFoundException>( () => GameLanguage.Resolve( new[] { "English" }, "French" ) );
		Assert.ThrowsException<DirectoryNotFoundException>( () => GameLanguage.Resolve( new[] { "French", "German" }, null ) );
		Assert.ThrowsException<DirectoryNotFoundException>( () => GameLanguage.Resolve( new string[0], null ) );
	}
}
