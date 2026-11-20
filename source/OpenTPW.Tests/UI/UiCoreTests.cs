using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests.UI;

/// <summary>The shared UI core (OpenTPW.UI) against a real Theme Park World table and the retail rules it ports.</summary>
[TestClass]
public class UiCoreTests
{
	[TestMethod]
	public void ReadsTheRetailOptionsPageTable()
	{
		var table = UiTable.Read( UiTableTestData.OptionsPage, UiTableDialect.ThemeParkWorld );
		// The table ends exactly at its last opcode 5: 0x884 bytes, 269 records, 50 opcode-0 elements (evidence\INDEX.md
		// "全局选项"), plus 7 buttons built by opcode 8.
		Assert.AreEqual( UiTableTestData.OptionsPage.Length / 2, table.Length );
		Assert.AreEqual( 269, table.Records.Count );
		Assert.AreEqual( 50, table.Records.Count( r => r.Opcode == 0 ) );
		Assert.AreEqual( 57, Count( table.Root ) );
		Assert.AreEqual( new UiRect( 0, 0, 2048, 1536 ), table.Root.Rect );
		Assert.IsTrue( table.Records.Where( r => r.Opcode == 5 ).All( r => r.Value == "" ) );

		static int Count( UiElement e ) => 1 + e.Children.Sum( Count );
	}

	[TestMethod]
	public void ReadsEveryOpcodeAndRejectsUnknownOnes()
	{
		ushort[] words =
		[
			0, 1, 0x0001, 0, 0x0100, 0, 0, 0, 2048, 1536,          // root: type 1, flags 1, id 0x100
				0x11, 352, 0, 0x12, 0xffff, 0xffff,                 // values at +0x90 / +0x94
				6, 10, 10, 100, 100,                                // button id 1
					1, 0x7759, 0x1d4a,                              // model hash
					4, 2, 50, 50, 40,                               // circle shape
				5,
				0xC, 3, 200, 0, 300, 100,                           // button id 0x13 (column 3)
					4, 4, 3, 200, 0, 300, 0, 250, 100,              // polygon
				5,
				0x13, 400, 400, 500, 500,                           // TPI-only element class 5
				0xB, 2, 1, 2, 3, 4,
				0xD, 7, 8,
				3, 1, 2, 3, 4,
			5,
		5,                                                          // the top level ends with opcode 5 too
		];
		var table = UiTable.Read( words, UiTableDialect.ThemeParkInc );
		var root = table.Root;
		Assert.AreEqual( 352, root.Value17 );
		Assert.AreEqual( -1, root.Value18 );
		Assert.AreEqual( 3, root.Children.Count );
		var first = root.Find( 1 )!;
		Assert.AreEqual( 0x1d4a7759u, first.ModelHash );
		Assert.IsInstanceOfType<UiCircleShape>( first.Shape );
		Assert.AreEqual( 0x21u, first.Flags );
		Assert.IsInstanceOfType<UiPolygonShape>( root.Find( 0x13 )!.Shape );
		Assert.AreEqual( 5, root.Children[2].Type );
		Assert.AreEqual( 2, root.Points!.Count );
		Assert.AreEqual( ((short)7, (short)8), root.Point );
		Assert.AreEqual( new UiRect( 1, 2, 3, 4 ), root.ExtraRect );
		Assert.AreEqual( words.Length, table.Length );

		Assert.ThrowsExactly<System.IO.InvalidDataException>( () => UiTable.Read( words, UiTableDialect.ThemeParkWorld ) );
		Assert.ThrowsExactly<System.IO.InvalidDataException>( () => UiTable.Read( words.AsSpan( 0, 12 ), UiTableDialect.ThemeParkInc ) );
	}

	[TestMethod]
	public void HashesModelRootNamesLikeTheRetailRoutine()
	{
		// Pairs from OpenTPI's exe-ui-models.tsv (hash operands of the TPI intro table and the matching ui.wad roots).
		Assert.AreEqual( 0x1d4a7759u, UiModelName.Hash( "b_play" ) );
		Assert.AreEqual( 0xe53957f3u, UiModelName.Hash( "mugshot" ) );
		Assert.AreEqual( 0x9b7c3c40u, UiModelName.Hash( "!frame" ) );
		// Bytes above 0x7f are sign-extended before the XOR.
		Assert.AreEqual( unchecked((uint)(-23 * 47)), UiModelName.Hash( new byte[] { 0xe9 } ) );
	}

	[TestMethod]
	public void ShapesMatchTheRetailTests()
	{
		var rect = new UiRectShape( new UiRect( 10, 20, 30, 40 ) );
		Assert.IsTrue( rect.Contains( 10, 20 ) );
		Assert.IsFalse( rect.Contains( 30, 39 ) );
		Assert.IsFalse( rect.Contains( 29, 40 ) );
		var circle = new UiCircleShape( 0, 0, 10 );
		Assert.IsTrue( circle.Contains( 6, 7 ) );
		Assert.IsFalse( circle.Contains( 6, 8 ) ); // 36 + 64 = 100 is not < 100
		var ellipse = new UiEllipseShape( 0, 0, 20, 10 );
		Assert.IsTrue( ellipse.Contains( 19, 0 ) );
		Assert.IsFalse( ellipse.Contains( 0, 10 ) );
		var triangle = new UiPolygonShape( [(0, 0), (100, 0), (0, 100)] );
		Assert.IsTrue( triangle.Contains( 10, 10 ) );
		Assert.IsFalse( triangle.Contains( 60, 60 ) );
	}

	[TestMethod]
	public void HitTestPrefersTheLastChildAndSkipsDisabledOrHidden()
	{
		var root = new UiElement( 1, 1, 1, new UiRect( 0, 0, 100, 100 ), null );
		var a = Add( root, 2, new UiRect( 0, 0, 50, 50 ) );
		var b = Add( root, 3, new UiRect( 25, 25, 75, 75 ) );
		var passive = Add( root, 4, new UiRect( 80, 80, 90, 90 ), flags: 0 );
		Assert.AreSame( b, root.HitTest( 30, 30 ) );
		Assert.AreSame( a, root.HitTest( 10, 10 ) );
		Assert.AreSame( root, root.HitTest( 85, 85 ), "elements without a hit flag do not answer" );
		passive.Flags = UiElement.FlagHitMask & 0x10000000;
		Assert.AreSame( passive, root.HitTest( 85, 85 ), "a top flag bit also makes an element hit-testable" );
		Assert.IsTrue( b.SetEnabled( false ) );
		Assert.IsFalse( b.SetEnabled( false ) );
		Assert.AreSame( a, root.HitTest( 30, 30 ) );
		a.Hidden = true;
		Assert.AreSame( root, root.HitTest( 30, 30 ) );
	}

	[TestMethod]
	public void RoutesHoverAndClicksAButton()
	{
		var (input, root, button, log) = Setup( UiInputOptions.ThemeParkInc );
		input.PointerMove( 20, 20 );
		Assert.AreEqual( UiControlState.Hover, input.StateOf( button ) );
		input.PointerDown( 0, 20, 20 );
		Assert.AreEqual( UiControlState.Pressed, input.StateOf( button ) );
		input.PointerUp( 0, 20, 20 );
		Assert.AreEqual( UiControlState.Hover, input.StateOf( button ) );
		CollectionAssert.AreEqual( new[] { UiMessageKind.Enter, UiMessageKind.ButtonDown, UiMessageKind.ButtonUp, UiMessageKind.Click }, log.ToArray() );

		// Released outside: no click, and Theme Park Inc does not relight the hover.
		log.Clear();
		input.PointerDown( 0, 20, 20 );
		input.PointerMove( 90, 90 );
		Assert.AreEqual( UiControlState.Normal, input.StateOf( button ) );
		input.PointerUp( 0, 90, 90 );
		CollectionAssert.DoesNotContain( log, UiMessageKind.Click );
		Assert.AreEqual( UiControlState.Normal, input.StateOf( button ) );
		Assert.AreSame( root, input.Hovered );
	}

	[TestMethod]
	public void ThemeParkWorldRelightsHoverAfterAnOutsideRelease()
	{
		var (input, _, button, _) = Setup( UiInputOptions.ThemeParkWorld );
		input.PointerMove( 20, 20 );
		input.PointerDown( 0, 20, 20 );
		input.PointerMove( 90, 90 );
		input.PointerUp( 0, 90, 90 );
		Assert.AreEqual( UiControlState.Hover, input.StateOf( button ) );
	}

	[TestMethod]
	public void TogglesAndDisablesButtons()
	{
		var (input, _, button, _) = Setup( UiInputOptions.ThemeParkInc, UiElement.FlagInteractive | UiElement.FlagToggle );
		input.PointerMove( 20, 20 );
		input.PointerDown( 0, 20, 20 );
		input.PointerUp( 0, 20, 20 );
		Assert.IsTrue( input.IsSelected( button ) );
		Assert.AreEqual( UiControlState.HoverSelected, input.StateOf( button ) );
		input.PointerMove( 90, 90 );
		Assert.AreEqual( UiControlState.Selected, input.StateOf( button ) );
		button.SetEnabled( false );
		Assert.AreEqual( UiControlState.Disabled, input.StateOf( button ) );
	}

	[TestMethod]
	public void AutoRepeatReplacesTheClick()
	{
		var (input, _, button, log) = Setup( UiInputOptions.ThemeParkInc, UiElement.FlagInteractive | UiElement.FlagRepeat );
		input.Tick( 1000 );
		input.PointerMove( 20, 20 );
		input.PointerDown( 0, 20, 20 );
		input.Tick( 1499 );
		CollectionAssert.DoesNotContain( log, UiMessageKind.RepeatStart );
		input.Tick( 1500 );
		input.Tick( 1625 ); // interval is strictly greater than 125 ms
		input.Tick( 1626 );
		input.PointerUp( 0, 20, 20 );
		CollectionAssert.AreEqual( new[] { UiMessageKind.Enter, UiMessageKind.ButtonDown, UiMessageKind.RepeatStart, UiMessageKind.Repeat, UiMessageKind.ButtonUp }, log.ToArray() );
	}

	private static UiElement Add( UiElement parent, uint id, UiRect rect, int type = 2, uint flags = UiElement.FlagInteractive )
	{
		var e = new UiElement( type, flags, id, rect, parent );
		parent.Children.Add( e );
		return e;
	}

	private static (UiInput, UiElement, UiElement, List<UiMessageKind>) Setup( UiInputOptions options, uint flags = UiElement.FlagInteractive )
	{
		var root = new UiElement( 1, 1, 1, new UiRect( 0, 0, 100, 100 ), null );
		var button = Add( root, 2, new UiRect( 10, 10, 30, 30 ), flags: flags );
		var input = new UiInput( options ) { Root = root };
		var log = new List<UiMessageKind>();
		input.Message += m => { if ( m.Target == button ) log.Add( m.Kind ); };
		return (input, root, button, log);
	}
}
