namespace OpenTPW.UI;

/// <summary>
/// How a game composes and times its <c>Data\Init\&lt;resolution&gt;\</c> startup screens in
/// <c>StartupImages_Prepare</c> (US 0x587380 / TPI 0x6f89b0).
/// </summary>
public enum StartupImageLayout
{
	/// <summary>
	/// Theme Park World (US 0x587380): initial startup shows <c>splash_&lt;lang&gt;</c> for 2500 ms (0x9c4 loop at
	/// US 0x587975), then <c>welcome</c> with <c>legal_&lt;lang&gt;</c> at a width-switched Y offset (US 0x587867) for
	/// 3000 ms (<c>StartupImages_WaitAndRelease</c>, US 0x5879c0). Later level loads composite <c>welcome_&lt;lang&gt;</c>
	/// onto <c>welcome</c> at a second width-switched Y offset (US 0x5878d6).
	/// </summary>
	SteppedByWidth,

	/// <summary>
	/// Theme Park Inc (TPI 0x6f89b0, <c>StartupImages_Step</c> TPI 0x6f99b0): State 9 shows <c>splash_&lt;lang&gt;</c>
	/// for 3000 ms (TPI 0x6f9a43), then switches to <c>welcome</c> composited with <c>legal_&lt;lang&gt;</c>,
	/// <c>welcome_&lt;lang&gt;</c> and <c>bull_&lt;lang&gt;</c> at proportional coordinates (TPI 0x6f9045..0x6f90d5) for
	/// 3000 ms (<c>StartupImages_WaitAndRelease</c>, TPI 0x6f92e0).
	/// </summary>
	ProportionalCombined,
}

/// <summary>
/// Parameters that distinguish the two games' <c>StartupImages_Prepare</c> (US 0x587380 / TPI 0x6f89b0) routines.
/// </summary>
public sealed record StartupImageOptions
{
	/// <summary>
	/// Display widths that map directly to a <c>&lt;width&gt;\</c> subdirectory (US 0x58747c: 400..1024;
	/// TPI 0x6f8aa0: 400..1280). Any other width falls back to <see cref="DefaultWidth"/>.
	/// </summary>
	public required IReadOnlyList<int> SupportedWidths { get; init; }

	/// <summary>Fallback directory width when the display width is not in <see cref="SupportedWidths"/> (US 0x587495 / TPI 0x6f8ac0).</summary>
	public int DefaultWidth { get; init; } = 640;

	/// <summary>Fallback language when the caller passes null or empty (US 0x587422 / TPI 0x6f8a46: <c>"english"</c>).</summary>
	public string DefaultLanguage { get; init; } = "english";

	/// <summary>File extension including the leading dot (<c>".tga"</c> in TPW, <c>".png"</c> in TPI).</summary>
	public required string Extension { get; init; }

	/// <summary>Overlay composition and stage layout rule.</summary>
	public required StartupImageLayout Layout { get; init; }

	/// <summary>Minimum milliseconds the splash screen stays up before transitioning (US 0x587990: 2500; TPI 0x6f9a43: 3000).</summary>
	public required int SplashDurationMs { get; init; }

	/// <summary>Minimum milliseconds the welcome screen stays up before release (US 0x5879c0 / TPI 0x6f92e0: 3000).</summary>
	public int WelcomeDurationMs { get; init; } = 3000;

	public static StartupImageOptions ThemeParkWorld { get; } = new()
	{
		SupportedWidths = [400, 512, 640, 800, 1024],
		Extension = ".tga",
		Layout = StartupImageLayout.SteppedByWidth,
		SplashDurationMs = 2500,
		WelcomeDurationMs = 3000,
	};

	public static StartupImageOptions ThemeParkInc { get; } = new()
	{
		SupportedWidths = [400, 512, 640, 800, 1024, 1280],
		Extension = ".png",
		Layout = StartupImageLayout.ProportionalCombined,
		SplashDurationMs = 3000,
		WelcomeDurationMs = 3000,
	};
}

/// <summary>
/// One overlay image composited onto the base <c>welcome</c> image by <c>StartupImage_Composite</c>
/// (US 0x586f40 / TPI 0x6f8260). Coordinates are top-down pixel offsets from the top-left corner of the base image;
/// the retail bottom-up buffer converts <see cref="Y"/> to <c>baseHeight - overlayHeight - Y</c> (US 0x587054 / TPI 0x6f8368).
/// Missing overlay files are skipped without error (US 0x5870f5 / TPI 0x6f841a).
/// </summary>
public readonly record struct StartupImageOverlay( string Path, int X, int Y );

/// <summary>A startup screen: a base image, optional overlays applied in order, and its minimum display duration.</summary>
public sealed record StartupImageScreen( string BasePath, IReadOnlyList<StartupImageOverlay> Overlays, int MinDurationMs );

/// <summary>
/// Resolved <c>Data\Init\&lt;resolution&gt;\</c> file paths and screen composition for one display width and language.
/// </summary>
public sealed class StartupImagePlan
{
	public StartupImageOptions Options { get; }
	public int ResolutionDirectory { get; }
	public string Language { get; }
	public string DirectoryPath { get; }
	public string SplashPath { get; }
	public string WelcomeBasePath { get; }
	public string LegalOverlayPath { get; }
	public string WelcomeOverlayPath { get; }
	public string? BullOverlayPath { get; }
	public StartupImageScreen SplashScreen { get; }

	internal StartupImagePlan( StartupImageOptions options, int resolutionDirectory, string language, string directoryPath )
	{
		Options = options;
		ResolutionDirectory = resolutionDirectory;
		Language = language;
		DirectoryPath = directoryPath;
		string ext = options.Extension;
		SplashPath = Path.Combine( directoryPath, $"splash_{language}{ext}" );
		WelcomeBasePath = Path.Combine( directoryPath, $"welcome{ext}" );
		LegalOverlayPath = Path.Combine( directoryPath, $"legal_{language}{ext}" );
		WelcomeOverlayPath = Path.Combine( directoryPath, $"welcome_{language}{ext}" );
		BullOverlayPath = options.Layout == StartupImageLayout.ProportionalCombined
			? Path.Combine( directoryPath, $"bull_{language}{ext}" )
			: null;
		SplashScreen = new StartupImageScreen( SplashPath, [], options.SplashDurationMs );
	}

	/// <summary>
	/// Builds the initial startup welcome screen for a loaded <c>welcome</c> base image of size
	/// (<paramref name="baseWidth"/>, <paramref name="baseHeight"/>).
	/// </summary>
	public StartupImageScreen InitialWelcomeScreen( int baseWidth, int baseHeight )
	{
		if ( Options.Layout == StartupImageLayout.SteppedByWidth )
		{
			return new StartupImageScreen(
				WelcomeBasePath,
				[new StartupImageOverlay( LegalOverlayPath, 0, StartupImages.LegalOverlayY( baseWidth ) )],
				Options.WelcomeDurationMs );
		}

		var (legalX, legalY) = StartupImages.ProportionalLegalOffset( baseWidth, baseHeight );
		int bullY = StartupImages.ProportionalBullOffsetY( baseHeight );
		return new StartupImageScreen(
			WelcomeBasePath,
			[
				new StartupImageOverlay( LegalOverlayPath, legalX, legalY ),
				new StartupImageOverlay( WelcomeOverlayPath, 0, 0 ),
				new StartupImageOverlay( BullOverlayPath!, 0, bullY ),
			],
			Options.WelcomeDurationMs );
	}

	/// <summary>
	/// Builds the subsequent level-load welcome screen (<c>DAT_008c478c != 0</c> at US 0x5878d6 in TPW; identical to
	/// <see cref="InitialWelcomeScreen"/> in TPI).
	/// </summary>
	public StartupImageScreen LoadWelcomeScreen( int baseWidth, int baseHeight )
	{
		if ( Options.Layout == StartupImageLayout.SteppedByWidth )
		{
			return new StartupImageScreen(
				WelcomeBasePath,
				[new StartupImageOverlay( WelcomeOverlayPath, 0, StartupImages.WelcomeOverlayY( baseWidth ) )],
				0 );
		}

		return InitialWelcomeScreen( baseWidth, baseHeight );
	}
}

/// <summary>
/// Selects <c>Data\Init\&lt;resolution&gt;\</c> startup image paths and overlay coordinates per
/// <c>StartupImages_Prepare</c> (US 0x587380 / TPI 0x6f89b0).
/// </summary>
public sealed class StartupImages( StartupImageOptions options )
{
	public StartupImageOptions Options { get; } = options;

	/// <summary>
	/// Picks the resolution directory number for <paramref name="displayWidth"/>: matches against
	/// <see cref="StartupImageOptions.SupportedWidths"/> and falls back to <see cref="StartupImageOptions.DefaultWidth"/>
	/// (US 0x58747c..0x5874a5 / TPI 0x6f8aa0..0x6f8ad0).
	/// </summary>
	public int SelectResolutionDirectory( int displayWidth )
	{
		for ( int i = 0; i < Options.SupportedWidths.Count; i++ )
			if ( Options.SupportedWidths[i] == displayWidth ) return displayWidth;
		return Options.DefaultWidth;
	}

	/// <summary>
	/// Resolves the language token used in startup image filenames (US 0x587422 / TPI 0x6f8a46: null or empty falls
	/// back to <c>"english"</c>; a non-empty language is used as-is with no per-file fallback).
	/// </summary>
	public string ResolveLanguage( string? language )
		=> string.IsNullOrEmpty( language ) ? Options.DefaultLanguage : language;

	/// <summary>
	/// Builds the startup image plan for <paramref name="displayWidth"/> and <paramref name="language"/> under
	/// <paramref name="initRoot"/> (default <c>Data\Init</c>).
	/// </summary>
	public StartupImagePlan Select( int displayWidth, string? language, string initRoot = @"Data\Init" )
	{
		int res = SelectResolutionDirectory( displayWidth );
		string lang = ResolveLanguage( language );
		string dir = Path.Combine( initRoot, res.ToString( System.Globalization.CultureInfo.InvariantCulture ) );
		return new StartupImagePlan( Options, res, lang, dir );
	}

	/// <summary>
	/// TPW width-switched Y offset for <c>legal_&lt;lang&gt;.tga</c> on the initial startup screen (US 0x587867..0x5878cc).
	/// </summary>
	public static int LegalOverlayY( int baseWidth ) => baseWidth switch
	{
		400 => 255,  // 0xff
		512 => 325,  // 0x145
		800 => 510,  // 0x1fe
		1024 => 652, // 0x28c
		_ => 408,    // 0x198 (640 and default)
	};

	/// <summary>
	/// TPW width-switched Y offset for <c>welcome_&lt;lang&gt;.tga</c> on subsequent load screens (US 0x5878d6..0x587936).
	/// </summary>
	public static int WelcomeOverlayY( int baseWidth ) => baseWidth switch
	{
		400 => 140,  // 0x8c
		512 => 179,  // 0xb3
		800 => 280,  // 0x118
		1024 => 358, // 0x166
		_ => 224,    // 0xe0 (640 and default)
	};

	/// <summary>
	/// TPI proportional offset for <c>legal_&lt;lang&gt;.png</c>:
	/// <c>x = (int)(W * (1/640f) * 320f)</c>, <c>y = (int)(H * (1/480f) * 384f)</c> (TPI 0x6f9045..0x6f9093).
	/// </summary>
	public static (int X, int Y) ProportionalLegalOffset( int baseWidth, int baseHeight )
	{
		int x = (int)(baseWidth * (1.0f / 640.0f) * 320.0f);
		int y = (int)(baseHeight * (1.0f / 480.0f) * 384.0f);
		return (x, y);
	}

	/// <summary>
	/// TPI proportional Y offset for <c>bull_&lt;lang&gt;.png</c>:
	/// <c>y = (int)(H * (1/480f) * 336f)</c> at <c>x = 0</c> (TPI 0x6f90a8..0x6f90d5).
	/// </summary>
	public static int ProportionalBullOffsetY( int baseHeight )
		=> (int)(baseHeight * (1.0f / 480.0f) * 336.0f);
}
