using System.Diagnostics;
using System.Security.Cryptography;

namespace OpenTPW;

internal static class MsdfFontCache
{
	private const string GeneratorVersion = "v1.4";
	private const int AtlasEmSize = 48;
	private const int DistanceRangePixels = 6;
	private const int GenerationTimeoutMilliseconds = 60_000;
	// Basic Latin, Latin extensions, punctuation, and the euro sign.
	private const string CharacterRanges = "[0x20,0x7E],[0xA0,0x2FF],[0x2000,0x206F],0x20AC";
	private static readonly string CacheVersion = $"{GeneratorVersion}-{AtlasEmSize}px-range{DistanceRangePixels}-top-latin";
	internal sealed record Asset( MsdfAtlas Atlas, string ImagePath );

	private static readonly Dictionary<string, Asset> Atlases = new( StringComparer.OrdinalIgnoreCase );
	private static readonly Dictionary<GameFont, Bf4Metrics> Metrics = [];

	public static Bf4Metrics GetMetrics( GameFont font )
	{
		if ( !Metrics.TryGetValue( font, out var metrics ) )
		{
			metrics = new( File.ReadAllBytes( GameDir.GetPath( $"Data/Language/English/{font.ResourceName}.bf4" ) ) );
			Metrics.Add( font, metrics );
		}

		return metrics;
	}

	public static Asset GetAtlas( GameFont font )
	{
		if ( Atlases.TryGetValue( font.FaceFile, out var asset ) )
		{
			return asset;
		}

		byte[] face = LoadFace( font );
		string hash = Convert.ToHexString( SHA256.HashData( face ) );
		// Include the generator and generation settings in the key when changing them.
		string directory = Path.Combine( ".opentpw", "msdf", CacheVersion, hash );
		Directory.CreateDirectory( directory );
		string imagePath = Path.GetFullPath( Path.Combine( directory, "atlas.png" ) );
		string jsonPath = Path.GetFullPath( Path.Combine( directory, "atlas.json" ) );
		if ( !File.Exists( imagePath ) || !File.Exists( jsonPath ) )
		{
			Generate( face, directory, imagePath, jsonPath );
		}

		asset = new( new MsdfAtlas( File.ReadAllText( jsonPath ) ), imagePath );
		Atlases.Add( font.FaceFile, asset );
		return asset;
	}

	private static byte[] LoadFace( GameFont font )
	{
		if ( font.FromGameArchive )
		{
			using var archive = new WadArchive( GameDir.GetPath( "Data/fonts.wad" ) );
			var entry = archive.GetFile( font.FaceFile ) ?? throw new FileNotFoundException( $"Original font {font.FaceFile} is missing from fonts.wad." );
			return entry.GetData();
		}
		else
		{
			// Do not redistribute Windows fonts. Use the installed face, or a user-supplied copy.
			string path = Path.Combine( AppContext.BaseDirectory, "content", "fonts", "original", font.FaceFile );
			if ( !File.Exists( path ) )
			{
				path = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.Fonts ), font.FaceFile );
			}

			if ( !File.Exists( path ) )
			{
				throw new FileNotFoundException( $"Install the original font {font.FaceFile}, or place it in content/fonts/original.", path );
			}

			return File.ReadAllBytes( path );
		}
	}

	private static void Generate( byte[] face, string directory, string imagePath, string jsonPath )
	{
		string generator = Path.Combine( AppContext.BaseDirectory, "content", "fonts", "msdf", "msdf-atlas-gen.exe" );
		if ( !File.Exists( generator ) )
		{
			throw new FileNotFoundException( "The bundled MSDF atlas generator is missing.", generator );
		}

		string prefix = Path.GetFullPath( Path.Combine( directory, Guid.NewGuid().ToString( "N" ) ) );
		string source = prefix + ".ttf";
		string image = prefix + ".png";
		string json = prefix + ".json";
		try
		{
			File.WriteAllBytes( source, face );
			var start = new ProcessStartInfo( generator )
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			string[] arguments = [
				"-font",
				source,
				"-type",
				"msdf",
				"-size",
				AtlasEmSize.ToString( System.Globalization.CultureInfo.InvariantCulture ),
				"-pxrange",
				DistanceRangePixels.ToString( System.Globalization.CultureInfo.InvariantCulture ),
				"-chars",
				CharacterRanges,
				"-yorigin",
				"top",
				"-imageout",
				image,
				"-json",
				json
			];
			foreach ( string argument in arguments )
			{
				start.ArgumentList.Add( argument );
			}

			using var process = Process.Start( start ) ?? throw new InvalidOperationException( "Unable to start the MSDF atlas generator." );
			var output = process.StandardOutput.ReadToEndAsync();
			var errors = process.StandardError.ReadToEndAsync();
			if ( !process.WaitForExit( GenerationTimeoutMilliseconds ) )
			{
				process.Kill( entireProcessTree: true );
				process.WaitForExit();
				throw new TimeoutException( $"MSDF atlas generation exceeded {GenerationTimeoutMilliseconds / 1000} seconds." );
			}

			Task.WaitAll( output, errors );
			if ( process.ExitCode != 0 || !File.Exists( image ) || !File.Exists( json ) )
			{
				throw new InvalidOperationException( $"MSDF atlas generation failed: {errors.Result} {output.Result}" );
			}

			_ = new MsdfAtlas( File.ReadAllText( json ) );
			File.Move( image, imagePath, overwrite: true );
			File.Move( json, jsonPath, overwrite: true );
		}
		finally
		{
			foreach ( string path in new[] { source, image, json } )
			{
				if ( File.Exists( path ) )
				{
					File.Delete( path );
				}
			}
		}
	}
}
