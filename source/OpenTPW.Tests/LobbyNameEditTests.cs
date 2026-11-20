using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyNameEditTests
{
	[TestMethod]
	public void OpeningSelectsThePromptAndTypingReplacesItWithoutAppending()
	{
		var flow = new LobbyPlayerFlow( new LobbyPlayerProfile?[4] ); flow.Select( 0 );
		var edit = new LobbyNameEdit( flow );
		edit.Open( "請輸入您的姓名" );
		Assert.AreEqual( flow.Name.Length, edit.SelectionLength );
		edit.Insert( "名字/abc" );
		Assert.AreEqual( "名字abc", edit.Text ); Assert.AreEqual( 0, edit.SelectionLength );
		edit.MoveTo( 2 ); edit.Insert( "X" );
		Assert.AreEqual( "名字Xabc", edit.Text );
		edit.Move( -1, true ); edit.Delete( true );
		Assert.AreEqual( "名字abc", edit.Text );
		edit.MoveTo( 0 ); edit.Delete( true ); Assert.AreEqual( "名字abc", edit.Text );
		edit.Delete( false ); Assert.AreEqual( "字abc", edit.Text );
		edit.Open( "請輸入您的姓名" ); Assert.AreEqual( "請輸入您的姓名", edit.Text );
	}
	[TestMethod]
	public void ReplacingASelectionRespectsCapacityAndKeepsTheUnselectedSuffix()
	{
		var flow = new LobbyPlayerFlow( new LobbyPlayerProfile?[4] ); flow.Select( 0 );
		var edit = new LobbyNameEdit( flow ); edit.Open( "abcdefghijklmnop" );
		edit.MoveTo( 2 ); edit.MoveTo( 4, true ); edit.Insert( "12345" );
		Assert.AreEqual( "ab12efghijklmnop", edit.Text );
		Assert.AreEqual( 4, edit.Caret );
		edit.Insert( "ignored" ); Assert.AreEqual( "ab12efghijklmnop", edit.Text );
		edit.SelectAll(); edit.Insert( "/?:*" ); Assert.AreEqual( 16, edit.SelectionLength );
		edit.Delete( false ); Assert.AreEqual( "", edit.Text );
		edit.Move( -1 ); Assert.AreEqual( 0, edit.Caret );
	}
}
