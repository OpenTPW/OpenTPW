using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System;
using System.Collections.Generic;
using System.IO;

namespace OpenTPW.Tests.UI;

/// <summary>
/// Tests the shared startup image and movie rules (<see cref="StartupImages"/> and <see cref="StartupMovies"/>)
/// against both games' retail constants and the Theme Park World installation data.
/// </summary>
[TestClass]
public class StartupTests
{
	[TestMethod]
	public void ThemeParkWorldResolutionAndOverlayLayoutMatchRetail()
	{
		var startup = new StartupImages( StartupImageOptions.ThemeParkWorld );
		foreach ( int width in new[] { 400, 512, 640, 800, 1024 } )
			Assert.AreEqual( width, startup.SelectResolutionDirectory( width ) );

		// 1280 is only in Theme Park Inc; Theme Park World falls back to 640 (US 0x58747c..0x5874a5).
		Assert.AreEqual( 640, startup.SelectResolutionDirectory( 1280 ) );
		Assert.AreEqual( 640, startup.SelectResolutionDirectory( 320 ) );
		Assert.AreEqual( "english", startup.ResolveLanguage( null ) );
		Assert.AreEqual( "english", startup.ResolveLanguage( "" ) );
		Assert.AreEqual( "American", startup.ResolveLanguage( "American" ) );

		var plan = startup.Select( 800, "American", @"Data\Init" );
		Assert.AreEqual( 800, plan.ResolutionDirectory );
		Assert.AreEqual( @"Data\Init\800\splash_American.tga", plan.SplashPath );
		Assert.AreEqual( @"Data\Init\800\welcome.tga", plan.WelcomeBasePath );
		Assert.AreEqual( @"Data\Init\800\legal_American.tga", plan.LegalOverlayPath );
		Assert.AreEqual( @"Data\Init\800\welcome_American.tga", plan.WelcomeOverlayPath );
		Assert.IsNull( plan.BullOverlayPath );
		Assert.AreEqual( 2500, plan.SplashScreen.MinDurationMs );

		// Width-switched Y offsets for legal_<lang>.tga (US 0x587867) and welcome_<lang>.tga (US 0x5878d6).
		(int Width, int LegalY, int WelcomeY)[] expected =
		[
			(400, 255, 140),
			(512, 325, 179),
			(640, 408, 224),
			(800, 510, 280),
			(1024, 652, 358),
		];
		foreach ( var (w, legalY, welcomeY) in expected )
		{
			var initial = plan.InitialWelcomeScreen( w, w * 3 / 4 );
			Assert.AreEqual( 3000, initial.MinDurationMs );
			Assert.AreEqual( 1, initial.Overlays.Count );
			Assert.AreEqual( new StartupImageOverlay( plan.LegalOverlayPath, 0, legalY ), initial.Overlays[0] );

			var load = plan.LoadWelcomeScreen( w, w * 3 / 4 );
			Assert.AreEqual( 1, load.Overlays.Count );
			Assert.AreEqual( new StartupImageOverlay( plan.WelcomeOverlayPath, 0, welcomeY ), load.Overlays[0] );
		}
	}

	[TestMethod]
	public void ThemeParkIncResolutionAndProportionalLayoutMatchRetail()
	{
		var startup = new StartupImages( StartupImageOptions.ThemeParkInc );
		foreach ( int width in new[] { 400, 512, 640, 800, 1024, 1280 } )
			Assert.AreEqual( width, startup.SelectResolutionDirectory( width ) );
		Assert.AreEqual( 640, startup.SelectResolutionDirectory( 1600 ) );

		var plan = startup.Select( 1280, "TraditionalChinese", @"Data\Init" );
		Assert.AreEqual( 1280, plan.ResolutionDirectory );
		Assert.AreEqual( @"Data\Init\1280\splash_TraditionalChinese.png", plan.SplashPath );
		Assert.AreEqual( @"Data\Init\1280\welcome.png", plan.WelcomeBasePath );
		Assert.AreEqual( @"Data\Init\1280\legal_TraditionalChinese.png", plan.LegalOverlayPath );
		Assert.AreEqual( @"Data\Init\1280\welcome_TraditionalChinese.png", plan.WelcomeOverlayPath );
		Assert.AreEqual( @"Data\Init\1280\bull_TraditionalChinese.png", plan.BullOverlayPath );
		Assert.AreEqual( 3000, plan.SplashScreen.MinDurationMs );

		// Proportional coordinates (TPI 0x6f9045..0x6f90d5) across the four shipped resolution directories.
		(int W, int H, int LegalX, int LegalY, int BullY)[] expected =
		[
			(640, 480, 320, 384, 336),
			(800, 600, 400, 480, 420),
			(1024, 768, 512, 614, 537),
			(1280, 1024, 640, 819, 716),
		];
		foreach ( var (w, h, legalX, legalY, bullY) in expected )
		{
			var screen = plan.InitialWelcomeScreen( w, h );
			Assert.AreEqual( 3000, screen.MinDurationMs );
			CollectionAssert.AreEqual(
				new[]
				{
					new StartupImageOverlay( plan.LegalOverlayPath, legalX, legalY ),
					new StartupImageOverlay( plan.WelcomeOverlayPath, 0, 0 ),
					new StartupImageOverlay( plan.BullOverlayPath!, 0, bullY ),
				},
				new List<StartupImageOverlay>( screen.Overlays ) );
		}
	}

	[TestMethod]
	public void StartupMovieSelectionAndStateMachineFollowRetailRules()
	{
		var tpwMovies = new StartupMovies( StartupMovieOptions.ThemeParkWorld );
		Assert.AreEqual( @"Data\Movies\bf.tgq", tpwMovies.SelectFirstMovie( "American" ) );
		Assert.AreEqual( @"Data\Movies\bf.tgq", tpwMovies.SelectFirstMovie( "English" ) );

		var tpiMovies = new StartupMovies( StartupMovieOptions.ThemeParkInc );
		Assert.AreEqual( @"Data\Movies\bfSim.tgq", tpiMovies.SelectFirstMovie( "American" ) );
		Assert.AreEqual( @"Data\Movies\bfSim.tgq", tpiMovies.SelectFirstMovie( "american" ) );
		Assert.AreEqual( @"Data\Movies\bf.tgq", tpiMovies.SelectFirstMovie( "English" ) );
		Assert.AreEqual( @"Data\Movies\bf.tgq", tpiMovies.SelectFirstMovie( "TraditionalChinese" ) );

		string[] expectedPool = ["bub.tgq", "buc.tgq", "grav.tgq", "jug.tgq", "mir.tgq", "plan.tgq", "roc.tgq", "roll.tgq"];
		for ( int day = 1; day <= 31; day++ )
			Assert.AreEqual( Path.Combine( @"Data\Movies", expectedPool[day & 7] ), tpiMovies.SelectSecondMovie( day ) );

		// State 4 -> 5 -> 6 -> 7 -> 8 -> 9 when the first movie exists and plays for two ticks, and the second is absent.
		var started = new List<string>();
		int updatesRemaining = 0;
		var player = new FakeMoviePlayer(
			onStart: path => { started.Add( path ); updatesRemaining = 2; return true; },
			onUpdate: () => --updatesRemaining <= 0 );
		bool Exists( string path ) => path.EndsWith( "bfSim.tgq", StringComparison.OrdinalIgnoreCase );

		var stage = StartupMovieStage.State4Enter;
		var trace = new List<StartupMovieStage> { stage };
		while ( stage != StartupMovieStage.State9Load )
		{
			stage = tpiMovies.Step( stage, "American", dayOfMonth: 14, Exists, player );
			trace.Add( stage );
		}
		CollectionAssert.AreEqual(
			new[]
			{
				StartupMovieStage.State4Enter,
				StartupMovieStage.State5PlayFirst,
				StartupMovieStage.State6WaitFirst,
				StartupMovieStage.State6WaitFirst,
				StartupMovieStage.State7PlaySecond,
				StartupMovieStage.State8WaitSecond,
				StartupMovieStage.State9Load,
			},
			trace );
		CollectionAssert.AreEqual( new[] { @"Data\Movies\bfSim.tgq" }, started );
	}

	[TestMethod]
	public void RetailThemeParkWorldInitAndMoviesMatchSelectionRules()
	{
		GameData.Require();
		string gamePath = GameData.GamePath!;
		string initRoot = Path.Combine( gamePath, "Data", "Init" );
		string moviesRoot = Path.Combine( gamePath, "Data", "Movies" );

		var startup = new StartupImages( StartupImageOptions.ThemeParkWorld );
		// Determine which language files are present in Data\Init\640 (American on US retail, English on TW/Euro).
		string initLang = File.Exists( Path.Combine( initRoot, "640", "Splash_American.tga" ) ) ? "American" : "English";
		foreach ( int width in new[] { 512, 640, 800, 1024 } )
		{
			var plan = startup.Select( width, initLang, initRoot );
			Assert.IsTrue( File.Exists( plan.SplashPath ), $"Missing {plan.SplashPath}" );
			Assert.IsTrue( File.Exists( plan.WelcomeBasePath ), $"Missing {plan.WelcomeBasePath}" );
			Assert.IsTrue( File.Exists( plan.LegalOverlayPath ), $"Missing {plan.LegalOverlayPath}" );
		}

		var movies = new StartupMovies( StartupMovieOptions.ThemeParkWorld );
		Assert.IsTrue( File.Exists( movies.SelectFirstMovie( initLang, moviesRoot ) ) );
		for ( int day = 0; day < 8; day++ )
		{
			string randomMovie = movies.SelectSecondMovie( day, moviesRoot );
			Assert.IsTrue( File.Exists( randomMovie ), $"Missing {randomMovie}" );
		}
	}

	private sealed class FakeMoviePlayer( Func<string, bool> onStart, Func<bool> onUpdate ) : IStartupMoviePlayer
	{
		public bool Start( string moviePath ) => onStart( moviePath );
		public bool Update() => onUpdate();
	}
}
