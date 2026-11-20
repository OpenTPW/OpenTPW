namespace OpenTPW.UI;

/// <summary>
/// Startup states 4–9 in the main state machine (<c>System_UpdateStateMachine</c>, US 0x54e360; TPI callback table
/// <c>0xb8bc30</c> initialized at TPI 0x6a0fb0, handlers TPI 0x6a2ff0..0x6a3150).
/// </summary>
public enum StartupMovieStage
{
	/// <summary>State 4 (TPI 0x6a2ff0): advances immediately to State 5.</summary>
	State4Enter = 4,
	/// <summary>State 5 (TPI 0x6a3020): runs <c>StartupMovie_Play</c> (US 0x54df20 / TPI 0x6a1780) and advances to State 6.</summary>
	State5PlayFirst = 5,
	/// <summary>State 6 (TPI 0x6a3070): polls <c>Movie_Update</c> (TPI 0x6515c0) until finished, then advances to State 7.</summary>
	State6WaitFirst = 6,
	/// <summary>State 7 (TPI 0x6a30c0): runs <c>StartupMovie_PlayRandom</c> (US 0x54e070 / TPI 0x6a1930) and advances to State 8.</summary>
	State7PlaySecond = 7,
	/// <summary>State 8 (TPI 0x6a3100): polls <c>Movie_Update</c> (TPI 0x6515c0) until finished, then advances to State 9.</summary>
	State8WaitSecond = 8,
	/// <summary>State 9 (TPI 0x6a3150; in TPW, after lobby or directly when movies finish): level / initial load with startup images.</summary>
	State9Load = 9,
}

/// <summary>
/// Host movie playback interface for startup states 5–8 (<c>Movie_Play</c>, US 0x51ae30 / TPI 0x651220;
/// <c>Movie_Update</c>, TPI 0x6515c0). TGQ decoding is implemented in Task 38; until then
/// <see cref="SkippedStartupMoviePlayer"/> treats movies as immediately finished.
/// </summary>
public interface IStartupMoviePlayer
{
	/// <summary>Starts playing <paramref name="moviePath"/> (<c>Movie_Play</c>, US 0x51ae30 / TPI 0x651220).</summary>
	bool Start( string moviePath );

	/// <summary>
	/// Advances movie playback by one frame (<c>Movie_Update</c>, TPI 0x6515c0). Returns <c>true</c> when playback
	/// has completed (or was skipped / not started) so the state machine transitions to the next state.
	/// </summary>
	bool Update();
}

/// <summary>Default no-op movie player used before Task 38 (TGQ decoder): treats every movie stage as skipped.</summary>
public sealed class SkippedStartupMoviePlayer : IStartupMoviePlayer
{
	public static SkippedStartupMoviePlayer Instance { get; } = new();
	public bool Start( string moviePath ) => false;
	public bool Update() => true;
}

/// <summary>
/// Parameters that distinguish the two games' startup movie selection (<c>StartupMovie_Play</c>, US 0x54df20 /
/// TPI 0x6a1780; <c>StartupMovie_PlayRandom</c>, US 0x54e070 / TPI 0x6a1930).
/// </summary>
public sealed record StartupMovieOptions
{
	/// <summary>
	/// The eight second-stage movie filenames indexed by <c>localtime()-&gt;tm_mday &amp; 7</c>
	/// (US 0x54e070 / TPI 0x6a1930).
	/// </summary>
	public static IReadOnlyList<string> DefaultRandomFiles { get; } =
	[
		"bub.tgq",
		"buc.tgq",
		"grav.tgq",
		"jug.tgq",
		"mir.tgq",
		"plan.tgq",
		"roc.tgq",
		"roll.tgq",
	];

	/// <summary>Default first-stage movie filename (<c>"bf.tgq"</c> at US 0x54df20 / TPI 0x6a1780).</summary>
	public string DefaultFirstMovieFile { get; init; } = "bf.tgq";

	/// <summary>
	/// Optional case-insensitive language overrides for the first-stage movie (in TPI 0x6a17b8,
	/// <c>_stricmp(lang, "American") == 0</c> selects <c>"bfSim.tgq"</c>).
	/// </summary>
	public IReadOnlyDictionary<string, string> FirstMovieByLanguage { get; init; } =
		new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );

	/// <summary>Eight-entry pool for the second-stage movie selected by <c>dayOfMonth &amp; 7</c>.</summary>
	public IReadOnlyList<string> RandomMovieFiles { get; init; } = DefaultRandomFiles;

	public static StartupMovieOptions ThemeParkWorld { get; } = new();

	public static StartupMovieOptions ThemeParkInc { get; } = new()
	{
		FirstMovieByLanguage = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase )
		{
			["American"] = "bfSim.tgq",
		},
	};
}

/// <summary>
/// Startup movie selection and state 4–8 progression (<c>StartupMovie_Play</c>, US 0x54df20 / TPI 0x6a1780;
/// <c>StartupMovie_PlayRandom</c>, US 0x54e070 / TPI 0x6a1930).
/// </summary>
public sealed class StartupMovies( StartupMovieOptions options )
{
	public StartupMovieOptions Options { get; } = options;

	/// <summary>
	/// Selects the Stage 1 (State 5) movie path under <paramref name="moviesRoot"/> (<c>StartupMovie_Play</c>,
	/// US 0x54df20 / TPI 0x6a1780).
	/// </summary>
	public string SelectFirstMovie( string? language, string moviesRoot = @"Data\Movies" )
	{
		string file = !string.IsNullOrEmpty( language ) && Options.FirstMovieByLanguage.TryGetValue( language, out var overrideFile )
			? overrideFile
			: Options.DefaultFirstMovieFile;
		return Path.Combine( moviesRoot, file );
	}

	/// <summary>
	/// Selects the Stage 2 (State 7) movie path under <paramref name="moviesRoot"/> from the day of the month
	/// (<c>tm_mday &amp; 7</c> in <c>StartupMovie_PlayRandom</c>, US 0x54e070 / TPI 0x6a1930).
	/// </summary>
	public string SelectSecondMovie( int dayOfMonth, string moviesRoot = @"Data\Movies" )
	{
		int index = dayOfMonth & 7;
		return Path.Combine( moviesRoot, Options.RandomMovieFiles[index] );
	}

	/// <summary>
	/// Advances one step of the startup movie state machine (States 4–8, TPI 0x6a2ff0..0x6a3100). Before Task 38,
	/// <paramref name="player"/> defaults to <see cref="SkippedStartupMoviePlayer.Instance"/>.
	/// </summary>
	public StartupMovieStage Step(
		StartupMovieStage stage,
		string? language,
		int dayOfMonth,
		Func<string, bool> fileExists,
		IStartupMoviePlayer? player = null,
		string moviesRoot = @"Data\Movies" )
	{
		player ??= SkippedStartupMoviePlayer.Instance;
		switch ( stage )
		{
			case StartupMovieStage.State4Enter:
				return StartupMovieStage.State5PlayFirst;
			case StartupMovieStage.State5PlayFirst:
			{
				string path = SelectFirstMovie( language, moviesRoot );
				if ( fileExists( path ) ) player.Start( path );
				return StartupMovieStage.State6WaitFirst;
			}
			case StartupMovieStage.State6WaitFirst:
				return player.Update() ? StartupMovieStage.State7PlaySecond : StartupMovieStage.State6WaitFirst;
			case StartupMovieStage.State7PlaySecond:
			{
				string path = SelectSecondMovie( dayOfMonth, moviesRoot );
				if ( fileExists( path ) ) player.Start( path );
				return StartupMovieStage.State8WaitSecond;
			}
			case StartupMovieStage.State8WaitSecond:
				return player.Update() ? StartupMovieStage.State9Load : StartupMovieStage.State8WaitSecond;
			default:
				return StartupMovieStage.State9Load;
		}
	}
}
