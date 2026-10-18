using OpenTPW.UI;
using System.Globalization;

namespace OpenTPW;

public class ParkScene
{
	private static readonly Dictionary<string, Model> Landscapes = new();
	private static readonly Dictionary<string, Model> Grounds = new();
	public const int MaxNameLength = 255;

	private readonly Action returnToLobby;
	private readonly Cursor cursor = new();
	private readonly Action[] loadingStages;
	private int loadedSteps;
	private string name;
	private SignFile? sign;
	private Texture? firstSignTexture;
	private Texture? secondSignTexture;

	private string LevelPath => $"levels/{Theme}";

	/// <summary>Changing the name regenerates the entrance sign on the render thread.</summary>
	public string Name
	{
		get => name;
		set
		{
			ArgumentNullException.ThrowIfNull( value );
			if ( string.IsNullOrWhiteSpace( value ) || value.Length > MaxNameLength || value.Any( char.IsControl ) )
			{
				throw new ArgumentException( $"Park names must contain 1–{MaxNameLength} printable characters.", nameof( value ) );
			}

			if ( name == value )
			{
				return;
			}

			if ( sign != null && firstSignTexture != null && secondSignTexture != null )
			{
				var pixels = ParkSign.Render( sign, value );
				firstSignTexture.UpdatePixels( pixels.First.Data );
				secondSignTexture.UpdatePixels( pixels.Second.Data );
			}

			name = value;
		}
	}

	public string Theme { get; }
	public PlayerProfile Player { get; }
	public RootPanel Hud { get; private set; } = null!;
	public SunActor SunLight { get; private set; } = null!;
	public MapFile Map { get; private set; } = null!;
	public ParkTerrainFile Terrain { get; private set; } = null!;
	public bool IsLoaded { get; private set; }
	public bool Paused { get; set; }
	public Vector3 Entrance { get; private set; }

	public ParkScene( int island, PlayerProfile player, Action returnToLobby, string? parkName = null )
	{
		var theme = ParkThemes.Get( island );
		Theme = theme.DirectoryName;
		Player = player;
		this.returnToLobby = returnToLobby;
		name = theme.DisplayName;
		if ( parkName != null )
		{
			Name = parkName;
		}

		// Keep the order here: the terrain supplies the entrance and ground mesh,
		// and the HUD depends on the entrance stage creating its root panel.
		loadingStages = [LoadMap, LoadTerrain, LoadLandscape, LoadGround, LoadEntrance, LoadHud];
	}

	/// <summary>Run one stage per frame, keeping GPU work and progress on the render thread.</summary>
	public bool AdvanceLoading()
	{
		if ( IsLoaded )
		{
			return true;
		}

		if ( loadedSteps < loadingStages.Length )
		{
			loadingStages[loadedSteps]();
			loadedSteps++;
			LoadingScreen.Progress = loadedSteps / (float)loadingStages.Length;
			return false;
		}

		IsLoaded = true;
		ParkCameraMode.StartPosition = Entrance;
		ParkCameraMode.Bounds = new Vector2( Terrain.OriginX + Terrain.Width * Terrain.CellWidth,
			Terrain.OriginY + Terrain.Height * Terrain.CellHeight );
		Camera.SetCameraMode<ParkCameraMode>();
		return true;
	}

	private void LoadMap() => Map = new MapFile( $"{LevelPath}/terrain/base.map" );

	private void LoadTerrain()
	{
		Terrain = new ParkTerrainFile( FileSystem.ReadAllBytes( $"{LevelPath}/terrain/base.md2" ),
			FileSystem.ReadAllBytes( $"{LevelPath}/terrain/base.lnd" ) );
		var settings = new SettingsFile( $"{LevelPath}/Standard.sam" );
		int entranceX = int.Parse( settings["FixedItemInfo.EntranceAPosX"], CultureInfo.InvariantCulture );
		int entranceY = int.Parse( settings["FixedItemInfo.EntranceAPosY"], CultureInfo.InvariantCulture );
		Entrance = Terrain.Position( entranceX, entranceY );
	}

	private void LoadLandscape()
	{
		SunLight = new SunActor { Position = new( 0, 100, 100 ) };
		if ( !Landscapes.TryGetValue( Theme, out var landscape ) )
		{
			landscape = new Model( $"{LevelPath}/terrain/base.md2", Material.World );
			Landscapes.Add( Theme, landscape );
		}

		_ = new StaticModelActor { Model = landscape };
	}

	private void LoadGround()
	{
		if ( Grounds.TryGetValue( Theme, out var ground ) )
		{
			_ = new StaticModelActor { Model = ground };
		}
		else
		{
			Grounds.Add( Theme, new ParkTerrainActor( Terrain ).Model! );
		}

		_ = new SkyActor();
	}

	private void LoadEntrance()
	{
		sign = new SignFile( FileSystem.ReadAllBytes( $"{LevelPath}/features/gates/gates.sgn" ) );
		var pixels = ParkSign.Render( sign, Name );
		firstSignTexture = new Texture( pixels.First.Data, SignFile.TextureSize, SignFile.TextureSize, TextureFlags.Wrap );
		secondSignTexture = new Texture( pixels.Second.Data, SignFile.TextureSize, SignFile.TextureSize, TextureFlags.Wrap );

		// Gate materials belong to this park; theme-only caching would share names.
		var gates = new Model( $"{LevelPath}/features/gates/gates.md2", Material.World, ResolveSignTexture );
		_ = new StaticModelActor { Model = gates };
		Hud = new RootPanel();
	}

	private Texture? ResolveSignTexture( string textureName )
	{
		if ( textureName.Equals( "sign1", StringComparison.OrdinalIgnoreCase ) )
		{
			return firstSignTexture;
		}

		if ( textureName.Equals( "sign2", StringComparison.OrdinalIgnoreCase ) )
		{
			return secondSignTexture;
		}

		return null;
	}

	private void LoadHud() => Hud.AddChild( new ParkHudPanel( this, returnToLobby ) );

	public void Update()
	{
		Actor.UpdateAll();
		cursor.Update();
	}

	public void Render() => Actor.RenderAll();

	public void RenderUI()
	{
		Actor.RenderUIAll();
		cursor.Draw();
	}
}
