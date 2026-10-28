using OpenTPW.UI;

namespace OpenTPW;

/// <summary>A model control fitted through the native 2048x1536 UI coordinate space.</summary>
internal sealed class LobbyUiModel : Panel
{
	/// <summary>One material batch of a prepared model state, with reusable projected vertices.</summary>
	private sealed record Part( Model Model, Vertex[] Vertices, System.Numerics.Vector3[] Positions, LobbyButtonGeometry Geometry );
	private readonly Dictionary<int, List<Part>> states = new();
	private static readonly Dictionary<string, LobbyButtonGeometry> geometries = new( StringComparer.OrdinalIgnoreCase );
	private static readonly Dictionary<string, Texture> textures = new( StringComparer.OrdinalIgnoreCase );
	private static Shader? shader;
	private readonly string modelName;
	public Rectangle Bounds { get; set; }
	public int State { get; set; }
	// 0x572440 maps frame bits 0x10/0x20 to texture bits 0x10000/0x20000;
	// 0x57dd90 -> 0x563c50 then selects D3DTADDRESS_WRAP instead of CLAMP.
	internal static SamplerType TextureSampler( uint frameFlags ) => (frameFlags & 0x30) != 0 ? SamplerType.PointWrap : SamplerType.Point;
	public LobbyUiModel( string modelName, Rectangle units )
	{
		this.modelName = modelName.Replace( '\\', '/' );
		Bounds = units;
		states.Add( 0, Build( 0 ) );
	}
	/// <summary>Prepare interactive states during scene construction, outside the frame loop.</summary>
	internal void Preload( params int[] values )
	{
		foreach ( int value in values ) if ( !states.ContainsKey( value ) ) states.Add( value, Build( value ) );
	}
	internal void PreloadAll()
	{
		var geometry = geometries[$"{modelName}:0"];
		Preload( Enumerable.Range( 0, geometry.StateRoots.Length ).ToArray() );
	}
	private List<Part> Build( int state )
	{
		string key = $"{modelName}:{state}";
		if ( !geometries.TryGetValue( key, out var geometry ) )
			geometries.Add( key, geometry = new( FileSystem.ReadAllBytes( AnimationFiles.ResolveModel(
				modelName.Contains( '/' ) ? modelName : $"ui/{modelName}.md2" ) ), state ) );
		var parts = new List<Part>();
		shader ??= new Shader( "content/shaders/ui.shader" );
		foreach ( int meshIndex in geometry.VisibleMeshes )
		{
			var mesh = geometry.Model.Meshes[meshIndex];
			var vertices = new Vertex[mesh.Vertices.Length];
			var positions = new System.Numerics.Vector3[vertices.Length];
			for ( int i = 0; i < vertices.Length; i++ )
			{
				positions[i] = System.Numerics.Vector3.Transform( mesh.Vertices[i].Position.GetSystemVector3(), mesh.WorldTransform );
				vertices[i] = new Vertex( Vector3.Zero, mesh.TexCoords[i] );
			}
			for ( int m = 0; m < mesh.Materials.Length; m++ )
			{
				var indices = new List<uint>();
				for ( int i = 0; i < mesh.Indices.Length; i += 3 )
					if ( mesh.Vertices[mesh.Indices[i]].TextureIndex == m ) indices.AddRange( mesh.Indices.Skip( i ).Take( 3 ) );
				if ( indices.Count == 0 ) continue;
				string name = mesh.Materials[m].Name?.TrimEnd( '\0' ) ?? throw new InvalidDataException( "UI model material has no texture" );
				if ( !textures.TryGetValue( name, out var texture ) )
					textures.Add( name, texture = new Texture( AnimationFiles.ResolveFile( $"ui/textures/{name}.wct", "UI model texture" ), TextureFlags.PointFilter ) );
				// Retail never culls in hardware (US 0x5668e1 sets CULLMODE 1) but drops back faces in software: 0x580530
				// emits a face only when 0x581320 finds a positive screen-space winding of its three transformed vertices.
				var material = new Material<ObjectUniformBuffer>( shader, MaterialFlags.DisableDepth );
				material.Set( "Color", texture, TextureSampler( mesh.Materials[m].Flags ) );
				parts.Add( new Part( new Model( vertices, indices.ToArray(), material ), (Vertex[])vertices.Clone(), positions, geometry ) );
			}
		}
		return parts;
	}
	protected override void OnRender()
	{
		if ( !states.TryGetValue( State, out var parts ) ) states.Add( State, parts = Build( State ) );
		var frame = new Rectangle( Bounds.X / 3.2f, Bounds.Y / 3.2f, Bounds.Width / 3.2f, Bounds.Height / 3.2f );
		foreach ( var part in parts )
		{
			for ( int i = 0; i < part.Vertices.Length; i++ ) part.Vertices[i].Position = part.Geometry.Project( part.Positions[i], frame );
			part.Model.UpdateVertices( part.Vertices );
			part.Model.Draw();
		}
	}
}
