using OpenTPW.UI;

namespace OpenTPW;

/// <summary>The advisor is registered in screen space independently of the world camera (0x429650).</summary>
internal sealed class LobbyAdvisorPanel : Panel
{
	private sealed record Part( int Mesh, Model Model, Vertex[] Vertices );
	private readonly List<Part> parts = new();
	private readonly Dictionary<int, int> mouths = new();
	private readonly LobbyAdvisor advisor;

	public LobbyAdvisorPanel( LobbyAdvisor advisor )
	{
		this.advisor = advisor;
		string[] names = [ "mouth - normal", "mouth - aah", "mouth - eee", "mouth - ooh", "mouth - sss" ];
		var shader = new Shader( "content/shaders/ui-lit.shader" );
		var textures = new Dictionary<string, Texture>( StringComparer.OrdinalIgnoreCase );
		var model = advisor.Geometry.Model;
		for ( int i = 0; i < names.Length; i++ )
		{
			// 0x44a560 calls the case-insensitive CRT comparison at 0x677f10.
			int mesh = model.Meshes.FindIndex( candidate => candidate.Name.Equals( names[i], StringComparison.OrdinalIgnoreCase ) );
			if ( mesh < 0 ) throw new InvalidDataException( $"Missing advisor mouth: {names[i]}" );
			mouths.Add( mesh, i + 1 );
		}
		for ( int meshIndex = 0; meshIndex < model.Meshes.Count; meshIndex++ )
		{
			var mesh = model.Meshes[meshIndex];
			for ( int m = 0; m < mesh.Materials.Length; m++ )
			{
				var indices = new List<uint>();
				for ( int i = 0; i < mesh.Indices.Length; i += 3 )
				{
					if ( mesh.Vertices[mesh.Indices[i]].TextureIndex == m ) indices.AddRange( mesh.Indices.Skip( i ).Take( 3 ) );
				}
				if ( indices.Count == 0 ) continue;
				string name = mesh.Materials[m].Name?.TrimEnd( '\0' ) ?? throw new InvalidDataException( "Advisor material has no texture" );
				if ( !textures.TryGetValue( name, out var texture ) )
					textures.Add( name, texture = new Texture( AnimationFiles.ResolveFile( $"global/advisor/textures/{name}.wct", "Advisor texture" ) ) );
				// Retail never culls in hardware (US 0x5668e1 sets CULLMODE 1) but drops back faces in software: 0x580530
				// emits a face only when 0x581320 finds a positive screen-space winding of its three transformed vertices.
				var material = new Material<ObjectUniformBuffer>( shader, MaterialFlags.DisableDepth );
				material.Set( "Color", texture );
				var vertices = new Vertex[mesh.Vertices.Length];
				parts.Add( new( meshIndex, new Model( vertices, indices.ToArray(), material ), vertices ) );
			}
		}
	}

	// Playback is advanced by Level, outside modal input routing (0x54a970).
	// This panel only projects the current pose; updating here would pause it in dialogs.
	protected override void OnDelete() => advisor.Cancel();
	protected override void OnRender()
	{
		if ( !advisor.Active ) return;
		var pose = advisor.Geometry.Pose( advisor.Animation, advisor.Frame );
		// LIGHTING=0 leaves vertices white like the scene models (0x44ce66).
		bool lit = LobbyQuality.Load().LightingEnabled;
		var lighting = Level.Current?.Lighting ?? LobbyLighting.Islands;
		var scene = LobbyLighting.RetailDirection;
		foreach ( var part in parts )
		{
			// Mouth meshes are selected by LIP state; all other nodes follow segment hiding.
			int index = part.Mesh;
			if ( mouths.TryGetValue( index, out int mouth ) ? mouth != advisor.Mouth : pose.Hidden[index] ) continue;
			var mesh = advisor.Geometry.Model.Meshes[index];
			for ( int i = 0; i < part.Vertices.Length; i++ )
			{
				var source = pose.Positions[index]?[i] ?? mesh.Vertices[i].Position;
				var point = System.Numerics.Vector3.Transform( source.GetSystemVector3(), pose.WorldTransforms[index] );
				var colour = lit ? LobbyAdvisorGeometry.Colour( lighting.Ambient, pose.WorldTransforms[index],
					mesh.Normals[i].GetSystemVector3(), scene, lighting.Diffuse ) : System.Numerics.Vector3.One;
				part.Vertices[i] = new Vertex( LobbyAdvisorGeometry.Project( point ), pose.TexCoords[index]?[i] ?? mesh.TexCoords[i] )
					{ Normal = new Vector3( colour.X, colour.Y, colour.Z ) };
			}
			part.Model.UpdateVertices( part.Vertices );
			part.Model.Draw();
		}
	}
}
