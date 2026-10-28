using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System;
using System.Collections.Generic;

namespace OpenTPW.Tests;

[TestClass]
[DoNotParallelize]
public class RenderLayerTests
{
	private List<Entity> previousEntities = null!;
	private RootPanel? previousHud;

	[TestInitialize]
	public void IsolateEntities()
	{
		previousEntities = Entity.All;
		previousHud = RootPanel.Instance;
		Entity.All = new List<Entity>();
		RootPanel.Instance = null;
	}

	[TestCleanup]
	public void RestoreEntities()
	{
		Entity.All = previousEntities;
		RootPanel.Instance = previousHud;
	}

	private sealed class WorldProbe : Entity
	{
		public Action? Draw;
		protected override void OnRender() => Draw?.Invoke();
	}

	private sealed class UiProbe : RootPanel
	{
		public Action? Draw;
		protected override void OnRender() => Draw?.Invoke();
	}

	private sealed class BackgroundProbe : Entity
	{
		public BackgroundProbe() => Layer = RenderLayer.Background;
	}

	[TestMethod]
	public void SkyBackgroundRendersBeforeWaterEvenWhenCreatedLater()
	{
		var water = new WorldProbe();
		var hud = new UiProbe();
		var sky = new BackgroundProbe();
		CollectionAssert.AreEqual( new Entity[] { sky, water, hud }, Entity.InRenderOrder( Entity.All ) );
	}

	[TestMethod]
	public void RootPanelIsUiLayerAndEntitiesDefaultToWorld()
	{
		Assert.AreEqual( RenderLayer.Ui, new RootPanel().Layer );
		Assert.AreEqual( RenderLayer.World, new WorldProbe().Layer );
	}

	[TestMethod]
	public void WorldEntitiesCreatedAfterUiStillRenderBeforeIt()
	{
		var calls = new List<string>();
		_ = new WorldProbe { Draw = () => calls.Add( "first world" ) };
		_ = new UiProbe { Draw = () => calls.Add( "first HUD" ) };
		_ = new WorldProbe { Draw = () => calls.Add( "spawned world" ) };
		_ = new UiProbe { Draw = () => calls.Add( "second HUD" ) };
		_ = new WorldProbe { Draw = () => calls.Add( "last world" ) };

		foreach ( var entity in Entity.InRenderOrder( Entity.All ) ) entity.Render();

		CollectionAssert.AreEqual( new[] { "first world", "spawned world", "last world", "first HUD", "second HUD" }, calls );
	}

	[TestMethod]
	public void EntitiesCreatedDuringRenderingJoinTheNextFrame()
	{
		var calls = new List<string>();
		bool spawned = false;
		_ = new UiProbe { Draw = () => calls.Add( "HUD" ) };
		_ = new WorldProbe { Draw = () =>
		{
			calls.Add( "world" );
			if ( spawned ) return;
			spawned = true;
			_ = new WorldProbe { Draw = () => calls.Add( "new world" ) };
		} };

		foreach ( var entity in Entity.InRenderOrder( Entity.All ) ) entity.Render();
		CollectionAssert.AreEqual( new[] { "world", "HUD" }, calls );
		calls.Clear();
		foreach ( var entity in Entity.InRenderOrder( Entity.All ) ) entity.Render();
		CollectionAssert.AreEqual( new[] { "world", "new world", "HUD" }, calls );
	}
}
