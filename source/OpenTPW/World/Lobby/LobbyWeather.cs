using StbImageSharp;
using N = System.Numerics;

namespace OpenTPW;

/// <summary>View-relative retail rain, seeded lightning strokes and the one-frame flash.</summary>
public sealed class LobbyWeather : ModelEntity
{
	private readonly Sky sky;
	private readonly Func<LobbyIslandConfig> currentIsland;
	private readonly LobbyConfig? config;
	private readonly Vector3[]? rainyCenters;
	private readonly LobbyQuality quality;
	private readonly LobbyWeatherSimulation simulation;
	private readonly LobbyRainSimulation rain = new();
	private readonly LobbyLightningGeometry lightning = new();
	private readonly Vertex[] rainVertices = new Vertex[400 * 6];
	private readonly Vertex[] boltVertices = new Vertex[65532];
	private readonly Model boltModel;
	private readonly Texture white;
	private readonly Material flashMaterial;
	public int RainAmount { get; private set; }

	public LobbyWeather( Sky sky, Func<LobbyIslandConfig> currentIsland, LobbyConfig? config = null )
		: this( sky, currentIsland, config, new LobbyRandom( unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()) ) ) { }

	internal LobbyWeather( Sky sky, Func<LobbyIslandConfig> currentIsland, LobbyConfig? config, LobbyRandom random, AudioSystem? audio = null )
	{
		this.sky = sky;
		this.currentIsland = currentIsland;
		this.config = config;
		rainyCenters = config?.Islands.Where( island => island.Rainy ).Select( IslandPosition ).ToArray();
		quality = LobbyQuality.Load();
		simulation = new LobbyWeatherSimulation( random );
		if ( audio != null )
		{
			simulation.ThunderRequested += () => audio.Play( "global/sound", "cat_globallobbysfx", 1 );
			simulation.AmbientRequested += id => audio.Play( $"levels/{currentIsland().ThemeName}/Sound", "cat_locallobbysfx", (uint)id );
		}
		// The view overlay follows the world, before the later HUD screen pass.
		Layer = RenderLayer.Ui;
		Model = CreateModel( rainVertices, "raindrop.tga" );
		boltModel = CreateModel( boltVertices, "lightning.tga" );
		white = new Texture( [255, 255, 255, 255], 1, 1 );
		// 0x57b360 draws a white full-screen quad with flags 0x9a1; blend 0xa1 is InverseDestinationColor/Zero,
		// so the flash inverts one frame rather than fading to white.
		flashMaterial = new Material( "content/shaders/ui.shader", MaterialFlags.DisableDepth | MaterialFlags.InvertDestination );
	}

	private static Model CreateModel( Vertex[] vertices, string name )
	{
		var image = ReadTexture( name );
		var material = new Material( "content/shaders/lobby-weather.shader", MaterialFlags.DisableDepth | MaterialFlags.DisableCulling | MaterialFlags.Additive );
		material.Set( "Color", new Texture( image.Data, image.Width, image.Height ) );
		return new Model( vertices, material );
	}

	internal static ImageResult ReadTexture( string name )
	{
		return RetailTexturePixels.DecodeTga( FileSystem.ReadAllBytes( "/generic/weather/" + name ) );
	}

	/// <summary>Island anchor used for lightning and rain distance; the origin only when no lobby config was given.</summary>
	private Vector3 IslandPosition( LobbyIslandConfig island )
	{
		if ( config == null ) return Vector3.Zero;
		if ( !config.CameraPositions.TryGetValue( island.Id, out var point ) )
			throw new InvalidDataException( $"Lobby config has no camera position for island {island.Id}." );
		return new( point.X, point.Y, island.Height );
	}

	protected override void OnUpdate()
	{
		var island = currentIsland();
		if ( island == null ) return;
		sky.SetIslandColour( island.SkyColour );
		simulation.Update( island.Lightning, IslandPosition( island ), RetailTime.FrameSeconds, quality.WeatherEnabled );
		var centers = rainyCenters ?? (island.Rainy ? new[] { IslandPosition( island ) } : Array.Empty<Vector3>());
		RainAmount = quality.WeatherEnabled ? CalculateRainAmount( Camera.Position, centers ) : 0;
		if ( RainAmount > 0 ) rain.Advance( RetailTime.FrameSeconds );
	}

	private static N.Vector4 Project( Vector3 point )
	{
		var clip = N.Vector4.Transform( new N.Vector4( point.GetSystemVector3(), 1 ), Camera.ViewMatrix * Camera.ProjMatrix );
		return new( clip.X, clip.Y, 0.001f, clip.W );
	}

	private static Vertex V( N.Vector4 p, float u, float v, float brightness, uint alpha ) =>
		new( new Vector3( p.X, p.Y, p.Z ), new Vector3( brightness ), new Vector2( u, v ) )
		{ TexIndex = BitConverter.SingleToInt32Bits( p.W ), MatFlags = alpha };

	private static void Quad( Vertex[] vertices, ref int offset, N.Vector4 a0, N.Vector4 a1, N.Vector4 b0, N.Vector4 b1, float brightness, uint startAlpha, uint endAlpha, bool reverseV = false )
	{
		if ( offset + 6 > vertices.Length ) throw new InvalidOperationException( "Lobby weather vertex capacity exceeded." );
		float startV = reverseV ? 1 : 0, endV = 1 - startV;
		vertices[offset++] = V( a0, 0, startV, brightness, startAlpha );
		vertices[offset++] = V( b0, 0, endV, brightness, endAlpha );
		vertices[offset++] = V( b1, 1, endV, brightness, endAlpha );
		vertices[offset++] = V( b1, 1, endV, brightness, endAlpha );
		vertices[offset++] = V( a1, 1, startV, brightness, startAlpha );
		vertices[offset++] = V( a0, 0, startV, brightness, startAlpha );
	}

	internal static void RainQuad( Vertex[] vertices, ref int offset, N.Vector4 tail, N.Vector4 head, float brightness )
	{
		var side = new N.Vector4( (head.Y - tail.Y) * 0.1f, -(head.X - tail.X) * 0.1f, 0, 0 );
		// 0x57b8ed..0x57b9fe: upper tail Vmax/alpha128, falling head Vmin/alpha255.
		Quad( vertices, ref offset, tail - side, tail + side, head - side, head + side,
			brightness, 128, 255, reverseV: true );
	}

	protected override void OnRender()
	{
		if ( simulation.ConsumeFlash() )
		{
			var material = flashMaterial;
			material.Set( "Color", white );
			using ( new Graphics.Scope( new Point2( 1, 1 ) ) ) Graphics.Quad( new Rectangle( 0, 0, 1, 1 ), material );
		}
		Array.Clear( boltVertices );
		int boltOffset = 0;
		if ( quality.WeatherEnabled )
		foreach ( var bolt in simulation.Bolts.AsEnumerable().Reverse() )
		{
			float phase = (bolt.Milliseconds % 500) * 0.002f;
			int baseColor = phase >= 0.9f ? 192 - (int)((1 - phase) * 10 * -64) : 255 - (int)((0.9f - phase) * 1.11f * 64);
			float brightness = ((baseColor + (RetailRandom.Next() & 63)) & 255) / 255f;
			foreach ( var stroke in lightning.Generate( bolt.Top, bolt.Bottom, bolt.Seed ) )
			{
				var points = stroke.Points.Select( Project ).ToArray();
				var directions = new N.Vector2[points.Length - 1];
				float width = 1.3f / (stroke.Depth + 1);
				for ( int i = 0; i < directions.Length; i++ )
				{
					var d = new N.Vector2( points[i + 1].X - points[i].X, points[i + 1].Y - points[i].Y );
					directions[i] = d.LengthSquared() == 0 ? N.Vector2.Zero : N.Vector2.Normalize( d ) * width;
				}
				N.Vector4 Side( int i )
				{
					var tangent = i == 0 ? directions[0] : i == points.Length - 1 ? directions[^1] : directions[i - 1] + directions[i];
					if ( tangent.LengthSquared() != 0 ) tangent = N.Vector2.Normalize( tangent ) * width;
					return new( tangent.Y, -tangent.X, 0, 0 );
				}
				for ( int i = 0; i < points.Length - 1; i++ )
					// 0x57a320 submits the previous edge at Vmin and the next edge at Vmax.
					// 0x57ae20 traverses from the lower endpoint; unlike rain, this path does not reverse V.
					Quad( boltVertices, ref boltOffset, points[i] - Side( i ), points[i] + Side( i ), points[i + 1] - Side( i + 1 ), points[i + 1] + Side( i + 1 ), brightness, 255, 255 );
			}
		}
		boltModel.UpdateVertices( boltVertices );
		boltModel.Draw();
		Array.Clear( rainVertices );
		int rainOffset = 0;
		for ( int i = 0; i < RainAmount; i++ )
		{
			var local = rain.Point( i, Camera.Position );
			var a = Project( Camera.Position + local );
			if ( a.W <= 0 ) continue;
			// 0x57b5e0 uses the same final wind vector for the 0.1-unit head and cube motion.
			var b = Project( Camera.Position + local + LobbyRainSimulation.Direction * 0.1f );
			float brightness = Math.Max( 0, (int)((6 - a.W) * 12) ) * 3 / 255f;
			RainQuad( rainVertices, ref rainOffset, a, b, brightness );
		}
		Model!.UpdateVertices( rainVertices );
		Model.Draw();
	}

	internal static int CalculateRainAmount( float meanSquaredDistance, int rainyIslands )
	{
		if ( rainyIslands == 0 ) return 0;
		// 0x5dbe7f..0x5dbe90 squares the mean twice before the reverse divide.
		float squared = meanSquaredDistance * meanSquaredDistance;
		float amount = 1e17f / (squared * squared);
		return amount < 20 ? 0 : (int)Math.Min( 400, amount );
	}

	internal static int CalculateRainAmount( Vector3 camera, IReadOnlyList<Vector3> rainyCenters )
	{
		float sum = 0;
		foreach ( var center in rainyCenters )
		{
			float x = camera.X - center.X, y = camera.Y - center.Y;
			sum += x * x + y * y;
		}
		return CalculateRainAmount( rainyCenters.Count == 0 ? 0 : sum / rainyCenters.Count, rainyCenters.Count );
	}
}
