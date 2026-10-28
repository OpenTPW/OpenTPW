using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyPlayerFlowTests
{
	private static LobbyPlayerProfile?[] Slots( params string?[] names ) => names
		.Select( ( name, slot ) => name == null ? null : new LobbyPlayerProfile( slot, name, $"users/{slot + 1}{name}/" ) )
		.ToArray();

	private static LobbyPlayerFlow Occupied() => new( Slots( "One", null, "Three", null ) );

	[TestMethod]
	public void OccupiedSlotCommitsTheExistingProfileWithoutOpeningTheDialog()
	{
		var flow = Occupied();
		Assert.IsTrue( flow.Select( 2 ) );
		Assert.AreEqual( LobbyPlayerFlowState.Committed, flow.State );
		Assert.AreEqual( LobbyPlayerChoiceKind.Existing, flow.Result!.Kind );
		Assert.AreEqual( 2, flow.Result.Slot );
		Assert.AreEqual( "Three", flow.Result.Name );
		Assert.AreEqual( -1, flow.ActiveSlot );
	}

	[TestMethod]
	public void EmptySlotOpensTheNameDialogWithoutAChoiceYet()
	{
		var flow = Occupied();
		Assert.IsTrue( flow.Select( 1 ) );
		Assert.AreEqual( LobbyPlayerFlowState.Naming, flow.State );
		Assert.AreEqual( 1, flow.ActiveSlot );
		Assert.IsNull( flow.Result );
		Assert.IsFalse( flow.CanConfirm, "The name box starts empty, as 0x7e3bc0 does." );
	}

	[TestMethod]
	public void NameIsClearedOfCharactersTheRetailEditBoxRefuses()
	{
		var flow = Occupied();
		flow.Select( 1 );
		flow.Name = "a\\b/c*d?e:f|g<h>i\"j";
		Assert.AreEqual( "abcdefghij", flow.Name );
		flow.Name = "tab\tand\nline";
		Assert.AreEqual( "tabandline", flow.Name );
	}

	[TestMethod]
	public void NameStopsAtSixteenCharacters()
	{
		var flow = Occupied();
		flow.Select( 3 );
		flow.Name = new string( 'x', 30 );
		Assert.AreEqual( LobbyPlayerFlow.MaxNameLength, flow.Name.Length );
		Assert.AreEqual( 16, LobbyPlayerFlow.MaxNameLength );
	}

	[TestMethod]
	public void OnlyTrailingSpacesAreTrimmedBeforeTheNameIsUsed()
	{
		var flow = Occupied();
		flow.Select( 1 );
		flow.Name = "  two words   ";
		Assert.AreEqual( "  two words   ", flow.Name, "The box keeps what was typed." );
		Assert.AreEqual( "  two words", flow.ResolveName(), "0x4a4600 only strips trailing 0x20." );
	}

	[TestMethod]
	public void BlankNameKeepsTheDialogOpen()
	{
		var flow = Occupied();
		flow.Select( 1 );
		foreach ( string blank in new[] { "", "   ", "\t" } )
		{
			flow.Name = blank;
			Assert.IsFalse( flow.CanConfirm );
			Assert.IsFalse( flow.Confirm(), "0x4a4600 returns 0 for an empty name." );
			Assert.AreEqual( LobbyPlayerFlowState.Naming, flow.State );
			Assert.IsNull( flow.Result );
		}
	}

	[TestMethod]
	public void ConfirmingANewNameCommitsTheSlotAndTheTypedText()
	{
		var flow = Occupied();
		flow.Select( 3 );
		flow.Name = "Four  ";
		Assert.IsTrue( flow.Confirm() );
		Assert.AreEqual( LobbyPlayerFlowState.Committed, flow.State );
		Assert.AreEqual( LobbyPlayerChoiceKind.New, flow.Result!.Kind );
		Assert.AreEqual( 3, flow.Result.Slot );
		Assert.AreEqual( "Four", flow.Result.Name );
		Assert.AreEqual( -1, flow.ActiveSlot );
	}

	[TestMethod]
	public void CancellingReturnsToTheButtonsAndKeepsTheTypedName()
	{
		var flow = Occupied();
		flow.Select( 1 );
		flow.Name = "Draft";
		Assert.IsTrue( flow.Cancel() );
		Assert.AreEqual( LobbyPlayerFlowState.Browsing, flow.State );
		Assert.IsNull( flow.Result );
		Assert.AreEqual( "Draft", flow.Name, "Retail never clears 0x7e3bc0, so reopening shows the old text." );

		flow.Select( 1 );
		Assert.AreEqual( "Draft", flow.Name );
		Assert.IsTrue( flow.CanConfirm, "A leftover name is still usable." );
	}

	[TestMethod]
	public void AdoptingTheCreatedProfileLabelsItsSlot()
	{
		var flow = Occupied();
		flow.Select( 1 );
		flow.Name = "New";
		flow.Confirm();

		Assert.IsNull( flow.SlotLabel( 1 ) );
		Assert.IsFalse( flow.Adopt( new LobbyPlayerProfile( 2, "Wrong", "users/3Wrong/" ) ), "Only the chosen slot." );
		Assert.IsTrue( flow.Adopt( new LobbyPlayerProfile( 1, "New", "users/2New/" ) ) );
		Assert.AreEqual( "New", flow.SlotLabel( 1 ) );
		Assert.AreEqual( "users/2New/", flow[1]!.Directory );
	}

	[TestMethod]
	public void ExistingChoicesHaveNothingToAdopt()
	{
		var flow = Occupied();
		flow.Select( 0 );
		Assert.IsFalse( flow.Adopt( new LobbyPlayerProfile( 0, "Other", "users/1Other/" ) ) );
		Assert.AreEqual( "One", flow.SlotLabel( 0 ) );
	}

	[TestMethod]
	public void SlotClicksAreIgnoredWhileTheDialogIsOpen()
	{
		var flow = Occupied();
		flow.Select( 1 );
		Assert.IsFalse( flow.Select( 0 ) );
		Assert.AreEqual( LobbyPlayerFlowState.Naming, flow.State );
		Assert.AreEqual( 1, flow.ActiveSlot );
	}

	[TestMethod]
	public void ConfirmAndCancelOnlyApplyToTheDialog()
	{
		var flow = Occupied();
		flow.Name = "Text";
		Assert.IsFalse( flow.Confirm() );
		Assert.IsFalse( flow.Cancel() );
		Assert.AreEqual( LobbyPlayerFlowState.Browsing, flow.State );

		flow.Select( 0 );
		Assert.IsFalse( flow.Confirm(), "An existing profile is committed by the click, not by the dialog." );
		Assert.IsFalse( flow.Cancel() );
		Assert.AreEqual( LobbyPlayerFlowState.Committed, flow.State );
	}

	[TestMethod]
	public void BackReturnOnlyLeavesACommittedChoice()
	{
		var flow = Occupied();
		Assert.IsFalse( flow.Back() );

		flow.Select( 0 );
		Assert.IsTrue( flow.Back() );
		Assert.AreEqual( LobbyPlayerFlowState.Browsing, flow.State );
		Assert.IsNull( flow.Result );

		flow.Select( 1 );
		Assert.IsFalse( flow.Back(), "The dialog has its own cancel." );
	}

	[TestMethod]
	public void InvalidSlotIndicesAreRejected()
	{
		var flow = Occupied();
		Assert.IsFalse( flow.Select( -1 ) );
		Assert.IsFalse( flow.Select( LobbyPlayerFlow.SlotCount ) );
		Assert.AreEqual( LobbyPlayerFlowState.Browsing, flow.State );
		Assert.IsNull( flow[4] );
		Assert.AreEqual( 4, LobbyPlayerFlow.SlotCount );
	}

	[TestMethod]
	public void SlotTableMustCoverTheFourSlots()
	{
		Assert.ThrowsException<ArgumentException>( () => new LobbyPlayerFlow( Array.Empty<LobbyPlayerProfile?>() ) );
		Assert.ThrowsException<ArgumentException>( () => new LobbyPlayerFlow( new LobbyPlayerProfile?[5] ) );
	}

	[TestMethod]
	public void ReloadPicksUpProfilesCreatedOutsideTheLobby()
	{
		var flow = Occupied();
		flow.Reload( Slots( "One", "Two", "Three", "Four" ) );
		Assert.AreEqual( "Two", flow.SlotLabel( 1 ) );
		Assert.AreEqual( "Four", flow.SlotLabel( 3 ) );
	}

	[TestMethod]
	public void ResetDropsTheDialogWithoutTouchingSlots()
	{
		var flow = Occupied();
		flow.Select( 1 );
		flow.Reset();
		Assert.AreEqual( LobbyPlayerFlowState.Browsing, flow.State );
		Assert.AreEqual( -1, flow.ActiveSlot );
		Assert.AreEqual( "One", flow.SlotLabel( 0 ) );
		Assert.AreEqual( "Three", flow.SlotLabel( 2 ) );
	}

	[TestMethod]
	public void ReadUsesTheRetailSlotAssignment()
	{
		// The directory scan itself is covered by LobbyPlayerProfilesTests; here only the wiring.
		var loaded = LobbyPlayerProfiles.FromDirectories( new[] { "users/2Second" } );
		var flow = new LobbyPlayerFlow( loaded );
		Assert.AreEqual( "Second", flow.SlotLabel( 1 ) );
		Assert.IsNull( flow.SlotLabel( 0 ) );
		Assert.IsTrue( flow.Select( 1 ) );
		Assert.AreEqual( "Second", flow.Result!.Name );
	}
	[TestMethod]
	public void ReloadInvalidatesAnOpenDialogBeforeAnExternallyCreatedProfileCanBeOverwritten()
	{
		var flow = Occupied();
		flow.Select( 1 );
		flow.Name = "Draft";
		flow.Reload( Slots( "One", "External", "Three", null ) );
		Assert.AreEqual( LobbyPlayerFlowState.Browsing, flow.State );
		Assert.IsFalse( flow.Confirm() );
		Assert.IsNull( flow.Result );
		Assert.IsTrue( flow.Select( 1 ) );
		Assert.AreEqual( LobbyPlayerChoiceKind.Existing, flow.Result!.Kind );
		Assert.AreEqual( "External", flow.Result.Name );
	}

	[TestMethod]
	public void InvalidReloadLeavesThePreviousChoiceIntact()
	{
		var flow = Occupied();
		flow.Select( 0 );
		var invalid = Slots( "One", null, null, null );
		invalid[0] = new LobbyPlayerProfile( 3, "Misplaced", "users/4Misplaced/" );
		Assert.ThrowsException<ArgumentException>( () => new LobbyPlayerFlow( invalid ) );
		Assert.ThrowsException<ArgumentException>( () => flow.Reload( invalid ) );
		Assert.AreEqual( LobbyPlayerFlowState.Committed, flow.State );
		Assert.AreEqual( "One", flow.Result!.Name );
		flow.Reload( Slots( null, null, null, null ) );
		Assert.IsNull( flow.Result );
	}

	[TestMethod]
	public void AdoptRejectsAProfileWhoseNameDoesNotMatchTheConfirmedChoice()
	{
		var flow = Occupied();
		flow.Select( 1 );
		flow.Name = "Expected";
		flow.Confirm();
		Assert.IsFalse( flow.Adopt( new LobbyPlayerProfile( 1, "Other", "users/2Other/" ) ) );
		Assert.IsNull( flow[1] );
	}

	[TestMethod]
	public void ConfirmStoresTheTrimmedWideNameInTheReopenedDialog()
	{
		var flow = Occupied();
		flow.Select( 1 );
		flow.Name = "新玩家  ";
		flow.Confirm();
		Assert.AreEqual( "新玩家", flow.Name );
		flow.Back();
		flow.Select( 1 );
		Assert.AreEqual( "新玩家", flow.Name );
		flow.Name = new string( '園', 17 );
		Assert.AreEqual( new string( '園', 16 ), flow.Name );
	}

}
