using StbImageSharp;

namespace OpenTPW;

/// <summary>
/// Lobby sky. 0x5d3c20 -> 0x581820 always loads the fantasy sky.tga/sky_rgb.tga/sky_cyl.tga, for every island
/// (sky_rgb only feeds a colour table that the grid builder overwrites, see LobbySkyGeometry).
/// SKYQUALITY 0/1/2/4 is the cloud layer count (0x44cc60 -> 0x581c80); 0 draws neither clouds nor horizon.
/// </summary>
public class Sky : ModelEntity
{
	/// <summary>A retained sky layer whose visibility follows the live preset without rebuilding its model.</summary>
	private sealed class SkyPart : ModelEntity
	{
		public Func<bool> Enabled = null!;
		public SkyPart() => Layer = RenderLayer.Background;
		protected override void OnRender() { if ( Enabled() ) base.OnRender(); }
	}
	// 0x5d3c20 sets the global background/fog colour 0x8c5b74 to 0xff44ddff. Island changes never alter it;
	// SKYCOLOUR only recolours the first cloud layer.
	private static readonly Vector3 Background = new( 68 / 255f, 221 / 255f, 1 );
	private Texture? colorTexture;
	private Vector3 color = Background;
	private readonly List<ModelEntity> clouds = new();
	private ModelEntity? horizon;
	private LobbySkyGeometry geometry = null!;
	private readonly LobbySkyColour colourState = new();
	private int layerCount;
	private LobbyQuality quality = null!;
	private float flashStrength;
	public Vector3 Color { get => color; set => SetColor( value ); }

	public unsafe void SetColor( Vector3 rgb01 )
	{
		if ( color == rgb01 ) return;
		color = rgb01;
		if ( colorTexture == null ) return;
		byte* rgba = stackalloc byte[4];
		rgba[0] = (byte)(Math.Clamp( color.X, 0, 1 ) * 255);
		rgba[1] = (byte)(Math.Clamp( color.Y, 0, 1 ) * 255);
		rgba[2] = (byte)(Math.Clamp( color.Z, 0, 1 ) * 255);
		rgba[3] = 255;
		Device.UpdateTexture( colorTexture.NativeTexture, (IntPtr)rgba, 4, 0, 0, 0, 1, 1, 1, 0, 0 );
	}

	public void SetIslandColour( System.Numerics.Vector3 rgb255 ) => colourState.SetTarget( rgb255 );
	public void SetFlash( float strength ) => flashStrength = Math.Clamp( strength, 0, 1 );

	public override void Spawn()
	{
		base.Spawn();
		Layer = RenderLayer.Background;
		colorTexture = new Texture( [68, 221, 255, 255], 1, 1 );
		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader", MaterialFlags.DisableDepth );
		material.Set( "Color", colorTexture );
		Model = Primitives.Cube.GenerateModel( material );
		Scale = new Vector3( -10000f );

		quality = LobbyQuality.Load();
		layerCount = quality.SkyLayers;
		geometry = LobbySkyGeometry.Load();
		var cloudImage = LobbySkyGeometry.ReadTexture( "sky.tga" );
		var cloudTexture = new Texture( cloudImage.Data, cloudImage.Width, cloudImage.Height, TextureFlags.Wrap );
		// US 0x5863c0 submits layers 0..n-1. Their flags include 0x1000, which matches the D3D
		// renderer's immediate mask 0x3000 (0x5667d0). 0x565590 draws them immediately and marks the batches
		// already drawn, so the later depth sort never reverses these layers. Entities keep creation order.
		var parts = new SkyPart[4];
		for ( int layer = 0; layer < 4; layer++ )
		{
			var cloudMaterial = new Material<ObjectUniformBuffer>( "content/shaders/lobby-sky.shader", MaterialFlags.DisableDepth | MaterialFlags.DisableCulling );
			cloudMaterial.Set( "Color", cloudTexture );
			int index = layer;
			parts[layer] = new SkyPart { Enabled = () => index < quality.SkyLayers, Model = new Model( geometry.CloudVertices( layer ), geometry.CloudIndices, cloudMaterial ) };
		}
		clouds.AddRange( parts );
		var horizonImage = LobbySkyGeometry.ReadTexture( "sky_cyl.tga" );
		var horizonMaterial = new Material<ObjectUniformBuffer>( "content/shaders/lobby-sky.shader", MaterialFlags.DisableDepth | MaterialFlags.DisableCulling );
		horizonMaterial.Set( "Color", new Texture( horizonImage.Data, horizonImage.Width, horizonImage.Height, TextureFlags.Wrap ) );
		horizon = new SkyPart { Enabled = () => quality.SkyLayers != 0, Model = new Model( geometry.HorizonVertices(), LobbySkyGeometry.HorizonIndices(), horizonMaterial ) };
	}

	private bool replayed;
	protected override void OnUpdate()
	{
		// A fixed comparison view carrying the retail cloud state replays it once and keeps the clouds still,
		// so the frame matches the retail capture taken while its camera was frozen.
		if ( LobbyCameraMode.FixedViewSky is { } retail )
		{
			if ( replayed ) return;
			geometry.Replay( retail.Offsets, retail.Greys );
			if ( retail.Layers != quality.SkyLayers )
				Log.Warning( $"Fixed view sky has {retail.Layers} retail layers but SKYQUALITY gives {quality.SkyLayers}" );
			replayed = true;
			return;
		}
		geometry.Advance( RetailTime.FrameSeconds );
	}

	protected override void OnRender()
	{
		layerCount = quality.SkyLayers;
		// 0x5d4790 fills 0x8cae80 = sky object 0x8c9f20 + 0xf60: layer zero's colours.
		colourState.Step( layerCount > 1 );
		SetColor( Background * (1 - flashStrength) + Vector3.One * flashStrength );
		Position = Camera.Position;
		for ( int layer = 0; layer < clouds.Count; layer++ )
		{
			clouds[layer].Position = Camera.Position;
			clouds[layer].Model!.UpdateVertices( geometry.CloudVertices( layer, flashStrength,
				layerCount > 1 ? colourState.Rgb : Background ) );
		}
		if ( horizon != null )
		{
			horizon.Position = Camera.Position;
			horizon.Model!.UpdateVertices( geometry.HorizonVertices( flashStrength ) );
		}
		base.OnRender();
	}
}

/// <summary>Retail camera +0x44..0x58 stores 16.16 colours, updated once per rendered frame.</summary>
internal sealed class LobbySkyColour
{
	internal int Red { get; private set; }
	internal int Green { get; private set; }
	internal int Blue { get; private set; }
	internal Vector3 Rgb => new Vector3( Red >> 16, Green >> 16, Blue >> 16 ) / 255f;
	private int targetRed, targetGreen, targetBlue;
	internal void SetTarget( System.Numerics.Vector3 rgb )
	{
		targetRed = (int)rgb.X << 16;
		targetGreen = (int)rgb.Y << 16;
		targetBlue = (int)rgb.Z << 16;
	}
	internal void Step( bool enabled )
	{
		if ( !enabled || (Red == targetRed && Green == targetGreen && Blue == targetBlue) ) return;
		// The retail instruction at 0x5d47cf really reads +0x58 for red as well as blue.
		Red += (targetBlue - Red) >> 4;
		Green += (targetGreen - Green) >> 4;
		Blue += (targetBlue - Blue) >> 4;
	}
}

/// <summary>Geometry and UV rules from US 0x584ef0 / 0x585720 / 0x5863c0.</summary>
internal sealed class LobbySkyGeometry
{
	internal const string Root = "levels/fantasy/sky/";
	private const float Span = 2400, Height = 180, Radius = 720, RimHeight = 36;
	/// <summary>
	/// Per-vertex grey of every cloud layer. US 0x585720, which builds the cloud grid, writes
	/// (rand() &amp; 0x7f) | 0x80 to all channels of the same vertex in all four layer tables (+0xf60/+0x1360/+0x1760/
	/// +0x1b60), after 0x585ce0 filled them from sky_rgb.tga, so sky_rgb never reaches the screen. Layer zero is
	/// then replaced by the island colour (0x5d96c0). A new set is drawn each time the sky is built.
	/// </summary>
	private readonly byte[] greys = new byte[256];
	private readonly Vector2[] offsets = new Vector2[4];
	internal uint[] CloudIndices { get; }
	private LobbySkyGeometry( Random random )
	{
		for ( int i = 0; i < greys.Length; i++ ) greys[i] = (byte)(0x80 | random.Next( 0x80 ));
		var vertices = CloudVertices( 0 );
		var indices = new List<uint>();
		for ( uint x = 0; x < 15; x++ )
			for ( uint z = 0; z < 15; z++ )
			{
				uint index = x * 16 + z;
				Add( index + 1, index, index + 17 );
				Add( index, index + 16, index + 17 );
			}
		CloudIndices = indices.ToArray();
		void Add( uint a, uint b, uint c )
		{
			if ( vertices[a].Position.Z > -RimHeight && vertices[b].Position.Z > -RimHeight && vertices[c].Position.Z > -RimHeight )
				indices.AddRange( new[] { a, b, c } );
		}
	}
	internal static LobbySkyGeometry Load()
	{
		return new LobbySkyGeometry( new Random() );
	}
	internal static ImageResult ReadTexture( string name )
	{
		return RetailTexturePixels.DecodeTga( FileSystem.ReadAllBytes( Root + name ) );
	}
	internal void Advance( float seconds )
	{
		float dt = seconds * 25;
		if ( dt < -25 || dt > 25 ) dt = 0;
		for ( int layer = 0; layer < offsets.Length; layer++ )
		{
			offsets[layer] += new Vector2( 0.0008f + layer * 0.0005f, 0.00009f - layer * 0.00003f ) * dt;
			offsets[layer] = new Vector2( offsets[layer].X % 1, offsets[layer].Y % 1 );
		}
	}
	/// <summary>
	/// Replaces the scroll offsets and vertex greys with values read from the retail sky object. Retail writes the
	/// same grey to all four tables and later overwrites layer zero, so layers 1–3 must agree and supply the greys.
	/// </summary>
	internal void Replay( float[][] retailOffsets, int[][] retailGreys )
	{
		if ( retailOffsets is not { Length: 4 } || retailOffsets.Any( pair => pair is not { Length: 2 } || !float.IsFinite( pair[0] ) || !float.IsFinite( pair[1] ) ) )
			throw new InvalidDataException( "Retail sky offsets must be four finite U/V pairs" );
		if ( retailGreys is not { Length: 4 } || retailGreys.Any( table => table is not { Length: 256 } || table.Any( value => value is < 0 or > 255 ) ) )
			throw new InvalidDataException( "Retail sky greys must be four tables of 256 bytes" );
		if ( !retailGreys[1].SequenceEqual( retailGreys[2] ) || !retailGreys[1].SequenceEqual( retailGreys[3] ) )
			throw new InvalidDataException( "Retail sky layers 1-3 have different greys; one grey table cannot replay them" );
		for ( int layer = 0; layer < 4; layer++ ) offsets[layer] = new Vector2( retailOffsets[layer][0], retailOffsets[layer][1] );
		for ( int i = 0; i < greys.Length; i++ ) greys[i] = (byte)retailGreys[1][i];
	}
	/// <summary>
	/// US 0x585720 stores positions at x*16+z. US 0x5863c0 walks that array linearly, but computes U
	/// from the inner Z index and V from the outer X index, reading colours at z*16+x (0x58670f).
	/// </summary>
	internal Vertex[] CloudVertices( int layer, float flash = 0, Vector3? layerZeroColour = null )
	{
		var vertices = new Vertex[256];
		float step = (7 - layer) * MathF.PI / 112;
		for ( int x = 0; x < 16; x++ )
			for ( int z = 0; z < 16; z++ )
			{
				float px = x * Span / 15 - Span * 0.5f, pz = z * Span / 15 - Span * 0.5f;
				var tint = new Vector3( greys[z * 16 + x] / 255f );
				if ( layer == 0 && layerZeroColour.HasValue ) tint = layerZeroColour.Value;
				float u = z * step + offsets[layer].X, v = x * step + offsets[layer].Y;
				// US 0x5863c0 negates and cycles the U/V scale coefficients between layers.
				if ( layer is 0 or 1 ) u = -u;
				if ( layer is 1 or 2 ) v = -v;
				vertices[x * 16 + z] = new Vertex( new Vector3( px, pz, Height - MathF.Sqrt( px * px + pz * pz ) * 0.2f ),
					tint * (1 - flash) + Vector3.One * flash, new Vector2( u, v ) ) { MatFlags = (uint)(207 - layer * 56) };
			}
		return vertices;
	}
	internal Vertex[] HorizonVertices( float flash = 0 )
	{
		var vertices = new Vertex[72];
		var scales = new[] { 1f, 0.98f, 0.96f, 0.84f };
		var heights = new[] { RimHeight, RimHeight * 0.5f, 0, -RimHeight };
		var v = new[] { new[] { 0.99f, 0.84f, 0.67f, 0.51f }, new[] { 0.49f, 0.34f, 0.17f, 0.01f } };
		for ( int half = 0; half < 2; half++ )
			for ( int segment = 0; segment < 9; segment++ )
			{
				float angle = (half * 16 + segment * 2) * MathF.PI / 16;
				for ( int ring = 0; ring < 4; ring++ )
					vertices[half * 36 + segment * 4 + ring] = new Vertex(
						new Vector3( MathF.Cos( angle ) * Radius * scales[ring], MathF.Sin( angle ) * Radius * scales[ring], heights[ring] ),
						Vector3.One, new Vector2( segment / 8f, v[half][ring] ) ) { MatFlags = 255 };
			}
		return vertices;
	}
	internal static uint[] HorizonIndices()
	{
		var indices = new List<uint>();
		for ( uint half = 0; half < 2; half++ )
			for ( uint segment = 0; segment < 8; segment++ )
				for ( uint ring = 0; ring < 3; ring++ )
				{
					uint index = half * 36 + segment * 4 + ring;
					indices.AddRange( new[] { index, index + 5, index + 4, index, index + 1, index + 5 } );
				}
		return indices.ToArray();
	}
}
