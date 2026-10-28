using System.Numerics;

namespace OpenTPW;

/// <summary>A lobby model with shared MD2 vertex, UV and node animation playback.</summary>
public sealed class LobbyModel : Entity
{
	private static readonly Dictionary<string, ModelFile> ModelCache = new( StringComparer.OrdinalIgnoreCase );
	private static readonly Dictionary<string, ModelAnimation> AnimationCache = new( StringComparer.OrdinalIgnoreCase );
	private static readonly Dictionary<string, Texture> TextureCache = new( StringComparer.OrdinalIgnoreCase );
	private static Texture? whiteTexture;
	private static Shader? sharedShader;
	/// <summary>One MD2 mesh with its baked world transform; <c>Animated</c> marks meshes re-baked every frame.</summary>
	private sealed class Part
	{
		public int MeshIndex;
		public ModelFile.Mesh Mesh = null!;
		public ModelEntity Entity = null!;
		public Model Model = null!;
		public Vertex[] Vertices = null!;
		public Matrix4x4 World;
		public bool Animated;
		public bool LitEnabled;
		public Material<ObjectUniformBuffer> Material = null!;
		public Texture[] FullTextures = null!, SmallTextures = null!;
		public bool Small;
		public SamplerType Sampler;
		// Lighting inputs the current vertex colours were computed with.
		public LobbyLighting? LitWith;
		public Quaternion LitRotation;
		public Matrix4x4 LitWorld;
		public System.Numerics.Vector3 LitDirection;
	}

	private readonly ModelFile modelFile;
	private readonly LobbyQuality quality;
	private bool LightingEnabled => quality.LightingEnabled;
	private readonly List<Part> parts = new();
	private ModelAnimation? animation;
	private float startedAt;

	public bool Loop { get; set; } = true;
	public float PlaybackSpeed { get; set; } = 1;
	public bool AnimationFinished { get; private set; }
	public bool Visible { get; set; } = true;

	public LobbyModel( Vector3 position, string modelPath, string textureDirectory, string? animationPath = null, Func<string, Texture?>? textureResolver = null, LobbyQuality? quality = null )
	{
		Position = position;
		quality ??= LobbyQuality.Load();
		this.quality = quality;
		Rotation = Quaternion.Identity;
		// A missing model throws with its full path instead of failing later on a null stream.
		modelPath = AnimationFiles.ResolveModel( modelPath );
		if ( !ModelCache.TryGetValue( modelPath, out var loadedModel ) )
			ModelCache[modelPath] = loadedModel = new ModelFile( modelPath );
		modelFile = loadedModel;
		for ( int meshIndex = 0; meshIndex < modelFile.Meshes.Count; meshIndex++ )
		{
			var mesh = modelFile.Meshes[meshIndex];
			if ( mesh.Vertices.Length == 0 || mesh.Indices.Length == 0 ) continue;
			foreach ( var batch in LobbyModelBatches.Create( mesh ) )
			{
				var material = new Material<ObjectUniformBuffer>( sharedShader ??= new Shader( "content/shaders/lobby-model.shader" ) );
				Texture[] LoadTextures( string directory )
				{
					var textures = new Texture[16];
					for ( int i = 0; i < textures.Length; i++ )
					{
						int materialIndex = batch.FirstMaterial + i;
						var name = materialIndex < mesh.Materials.Length ? mesh.Materials[materialIndex].Name?.TrimEnd( '\0' ) : null;
						if ( string.IsNullOrEmpty( name ) )
							textures[i] = whiteTexture ??= Texture.Missing;
						else
						{
							var texture = textureResolver?.Invoke( name );
							if ( texture == null )
							{
								string texturePath = $"{directory}/{name}.wct";
								try
								{
									texturePath = AnimationFiles.ResolveFile( texturePath, "Texture" );
								}
								catch ( FileNotFoundException ) when ( name.Equals( "signgrab", StringComparison.OrdinalIgnoreCase ) )
								{
									// 0x572440 -> 0x571ea0 falls back to the cache's default texture.
									// 0x571060 initializes it from this shipped TGA, not a white pixel.
									Log.Warning( $"Missing lobby material {name} in {modelPath}: {texturePath}; using retail default" );
									texturePath = AnimationFiles.ResolveFile( "generic/defaulttexture/NotFound.tga", "Retail default texture" );
								}
								if ( texture == null && !TextureCache.TryGetValue( texturePath, out texture ) )
								{
									if ( texturePath.HasExtension( ".wct" ) ) texture = new Texture( texturePath, TextureFlags.Repeat );
									else
									{
										// Game-data TGAs use the virtual filesystem, unlike engine-content image paths.
										using var stream = FileSystem.OpenRead( texturePath );
										texture = new Texture( stream, TextureFlags.Repeat );
									}
									TextureCache[texturePath] = texture;
								}
							}
							textures[i] = texture;
						}
					}
					return textures;
				}
				var fullTextures = LoadTextures( textureDirectory );
				var smallDirectory = LobbyQuality.Load( "low" ).TextureDirectory( textureDirectory );
				var smallTextures = smallDirectory == textureDirectory ? fullTextures : LoadTextures( smallDirectory );
				var sampler = batch.Wrap ? SamplerType.AnisotropicWrap : SamplerType.Anisotropic;
				material.Set( "Color", quality.UseSmallTextures ? smallTextures : fullTextures, sampler );

				var vertices = new Vertex[mesh.Vertices.Length];
				for ( int i = 0; i < vertices.Length; i++ )
				{
					int textureIndex = (int)mesh.Vertices[i].TextureIndex;
					// Preserve authored UVs. WCT row order is paired with the shader's single V flip;
					// wrapping is selected per batch, not by modifying coordinates at load time.
					vertices[i].TexCoords = mesh.TexCoords[i];
					vertices[i].TexIndex = Math.Clamp( textureIndex - batch.FirstMaterial, 0, 15 );
					vertices[i].MatFlags = textureIndex < mesh.Materials.Length ? mesh.Materials[textureIndex].Flags : 0;
				}

				var part = new Part { MeshIndex = meshIndex, Mesh = mesh, Vertices = vertices, World = mesh.WorldTransform,
					Material = material, FullTextures = fullTextures, SmallTextures = smallTextures,
					Small = quality.UseSmallTextures, Sampler = sampler, LitEnabled = LightingEnabled };
				Bake( part, null );
				Light( part, CurrentLighting, Rotation, LightingEnabled );
				part.Model = new Model( vertices, batch.Indices, material );
				part.Entity = new ModelEntity { Model = part.Model, Position = Position, Rotation = Rotation, Scale = Scale };
				parts.Add( part );
			}
		}

		if ( animationPath != null )
			Play( animationPath );
	}

	/// <summary>
	/// Starts an animation file, normally one returned by <see cref="AnimationFiles.Resolve"/> or
	/// <see cref="AnimationFiles.Segments"/>. A missing file throws with its full path.
	/// </summary>
	public void Play( string animationPath, bool loop = true )
	{
		animationPath = AnimationFiles.ResolveFile( animationPath, "Animation" );
		if ( !AnimationCache.TryGetValue( animationPath, out animation ) )
			AnimationCache[animationPath] = animation = new ModelAnimation( animationPath );
		Loop = loop;
		startedAt = Time.Now;
		AnimationFinished = false;
		OnUpdate();
	}

	private static LobbyLighting CurrentLighting => Level.Current?.Lighting ?? LobbyLighting.Islands;

	private static void Bake( Part part, Vector3[]? positions )
	{
		for ( int i = 0; i < part.Vertices.Length; i++ )
		{
			var source = positions != null ? positions[i] : part.Mesh.Vertices[i].Position;
			var position = System.Numerics.Vector3.Transform( source.GetSystemVector3(), part.World );
			part.Vertices[i].Position = new Vector3( position.X, position.Z, position.Y );
		}
	}

	/// <summary>
	/// Writes the retail vertex colours (<see cref="LobbyLighting"/>) into the vertices' normal slot, which
	/// lobby-model.shader reads as colour. Prelit meshes keep their authored colours.
	/// </summary>
	private static void Light( Part part, LobbyLighting lighting, Quaternion rotation, bool enabled )
	{
		var prelit = part.Mesh.PrelitColours;
		if ( !enabled && prelit == null )
		{
			for ( int i = 0; i < part.Vertices.Length; i++ ) part.Vertices[i].Normal = Vector3.One;
			part.LitWith = lighting; part.LitRotation = rotation; part.LitWorld = part.World; part.LitDirection = LobbyLighting.WorldDirection;
			return;
		}
		var world = LobbyLighting.WorldDirection;
		var direction = prelit == null ? LobbyLighting.LocalDirection( world, part.World, rotation ) : default;
		for ( int i = 0; i < part.Vertices.Length; i++ )
		{
			var colour = prelit != null ? LobbyLighting.Rgb( prelit[i] ) : lighting.Colour( direction, part.Mesh.Normals[i].GetSystemVector3() );
			part.Vertices[i].Normal = new Vector3( colour.X, colour.Y, colour.Z );
		}
		part.LitWith = lighting; part.LitRotation = rotation; part.LitWorld = part.World; part.LitDirection = world;
	}

	protected override void OnUpdate()
	{
		// Pooled flock slots retain GPU resources but must not animate or upload invisible geometry.
		if ( !Visible )
		{
			foreach ( var part in parts ) part.Entity.Model = null;
			return;
		}
		ModelPose? pose = null;
		if ( animation != null )
		{
			float elapsedFrames = Math.Max( 0, Time.Now - startedAt ) * ModelAnimation.FramesPerSecond * Math.Max( 0, PlaybackSpeed );
			AnimationFinished = !Loop && elapsedFrames >= animation.Length;
			float frame = animation.StartFrame + (Loop && animation.Length > 0 ? elapsedFrames % animation.Length : Math.Min( elapsedFrames, animation.Length ));
			pose = animation.Evaluate( modelFile, frame );
		}

		var lighting = CurrentLighting;
		foreach ( var part in parts )
		{
			int i = part.MeshIndex;
			if ( part.Small != quality.UseSmallTextures )
			{
				part.Small = quality.UseSmallTextures;
				part.Material.Set( "Color", part.Small ? part.SmallTextures : part.FullTextures, part.Sampler );
			}
			part.Entity.Position = Position;
			part.Entity.Rotation = Rotation;
			part.Entity.Scale = Scale;
			part.Entity.Model = !Visible || (pose?.Hidden[i] ?? false) ? null : part.Model;
			bool changed = false;
			if ( pose != null )
			{
				var positions = pose.Positions[i];
				var texCoords = pose.TexCoords[i];
				bool moved = pose.WorldTransforms[i] != part.World;
				if ( moved || positions != null || texCoords != null || part.Animated )
				{
					part.World = pose.WorldTransforms[i];
					Bake( part, positions );
					texCoords ??= part.Mesh.TexCoords;
					for ( int j = 0; j < part.Vertices.Length; j++ )
						part.Vertices[j].TexCoords = texCoords[j];
					part.Animated = moved || positions != null || pose.TexCoords[i] != null;
					changed = true;
				}
			}
			// 0x577000 relights a mesh whenever the light version changes, which every camera update does
			// (0x5d3eb0 re-aims the light); node motion and the model's rotation also move it in node space.
			if ( part.LitEnabled != LightingEnabled || part.LitWith != lighting || part.LitDirection != LobbyLighting.WorldDirection
				|| part.LitRotation != Rotation || part.LitWorld != part.World )
			{
				part.LitEnabled = LightingEnabled;
				Light( part, lighting, Rotation, LightingEnabled );
				changed = true;
			}
			if ( changed ) part.Model.UpdateVertices( part.Vertices );
		}
	}

	protected override void OnDelete()
	{
		foreach ( var part in parts )
		{
			part.Entity.Model = null;
			part.Entity.Delete();
			All.Remove( part.Entity );
		}
	}
}
