namespace OpenTPW;

public partial class Model
{
	private readonly Dictionary<string, string> _animationPaths = new( StringComparer.OrdinalIgnoreCase );
	private readonly Dictionary<string, ModelFile.AnimationGraph> _clips = new( StringComparer.OrdinalIgnoreCase );
	private readonly List<AnimatedMesh> _animationMeshes = [];

	private sealed record AnimatedMesh( Mesh Mesh, ModelFile.MeshData Source, ModelGeometry.Batch Batch, int Node, int MeshIndex, Vector3[] Normals );

	private readonly HashSet<int> _previouslyAnimatedMeshes = [];
	private float _lastAnimationTime = Time.Now;
	private ulong _uploadedRevision;

	public ModelAnimationPlayer? Animation { get; private set; }

	public IEnumerable<string> AnimationNames => _animationPaths.Keys.Concat( _clips.Keys ).Distinct( StringComparer.OrdinalIgnoreCase );

	private void DiscoverAnimations( string path )
	{
		string directory = System.IO.Path.GetDirectoryName( path )?.Replace( '\\', '/' ) ?? "";
		foreach ( var (name, file) in ModelAnimationNames.FindCompanions( path, FileSystem.GetFiles( directory ) ) )
			_animationPaths[name] = file;
	}

	/// <summary>Play a companion suffix ("i", "m1", "c", etc.), or "default" for an embedded graph.</summary>
	    public bool PlayAnimation( string name, bool loop = true )
	{
		if ( Animation == null )
		{
			return false;
		}

		if ( !_clips.TryGetValue( name, out var clip ) )
		{
			if ( !_animationPaths.TryGetValue( name, out var path ) )
			{
				return false;
			}

			clip = new ModelFile( path ).Animation;
			if ( clip == null )
			{
				return false;
			}

			_clips.Add( name, clip );
		}

		Animation.Play( clip, loop );
		if ( Animation.HasExternalPathTracks )
		{
			Log.Warning( $"Animation {name} contains path-following tracks that need a ride route; only its self-contained channels are played." );
		}

		_lastAnimationTime = Time.Now;
		return true;
	}

	internal void PrepareAnimation()
	{
		if ( Animation == null )
		{
			return;
		}

		float delta = Math.Max( 0, Time.Now - _lastAnimationTime );
		_lastAnimationTime = Time.Now;
		if ( delta > 0 )
		{
			Animation.Advance( delta );
		}

		if ( _uploadedRevision == Animation.Revision )
		{
			return;
		}

		foreach ( var binding in _animationMeshes )
		{
			binding.Mesh.LocalMatrix = ModelGeometry.ToEngineMatrix( Animation.WorldMatrices[binding.Node] );
			binding.Mesh.Visible = Animation.Visible[binding.Node];
			bool deformed = Animation.DeformedMeshes[binding.MeshIndex];
			bool wasAnimated = _previouslyAnimatedMeshes.Contains( binding.MeshIndex );
			if ( deformed || Animation.AnimatedTextureMeshes[binding.MeshIndex] || wasAnimated )
			{
				UpdateAnimatedVertices( binding, Animation, deformed, wasAnimated );
			}
		}

		_previouslyAnimatedMeshes.Clear();
		for ( int i = 0; i < Animation.DeformedMeshes.Length; i++ )
		{
			if ( Animation.DeformedMeshes[i] || Animation.AnimatedTextureMeshes[i] )
			{
				_previouslyAnimatedMeshes.Add( i );
			}
		}

		_uploadedRevision = Animation.Revision;
	}

	private void UpdateAnimatedVertices( AnimatedMesh binding, ModelAnimationPlayer animation, bool deformed, bool wasAnimated )
	{
		if ( deformed )
		{
			ModelGeometry.UpdateDeformedVertices( binding.Source, binding.Batch, animation.Positions[binding.MeshIndex], binding.Normals );
		}

		for ( int i = 0; i < binding.Batch.Vertices.Length; i++ )
		{
			uint corner = binding.Batch.SourceCorners[i];
			if ( !deformed && wasAnimated )
			{
				var position = binding.Source.Vertices[corner].Position;
				var normal = binding.Source.Normals[corner];
				binding.Batch.Vertices[i].Position = ModelGeometry.ToEngineVector( position );
				binding.Batch.Vertices[i].Normal = ModelGeometry.ToEngineVector( normal );
			}

			binding.Batch.Vertices[i].TexCoords = animation.TexCoords[binding.MeshIndex][corner];
		}

		binding.Mesh.UpdateVertices( binding.Batch.Vertices );
	}
}
