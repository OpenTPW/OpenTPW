using System.Numerics;

namespace OpenTPW;

/// <summary>Per-instance MD2 pose and clock. Source geometry and clips are never modified.</summary>
public sealed partial class ModelAnimationPlayer
{
	private readonly ModelFile model;
	private readonly int[] parents;
	private readonly int[] evaluationOrder;
	private readonly int[] meshNodes;
	private readonly Matrix4x4[] local;
	private readonly Matrix4x4[] baseline;
	private readonly bool[] baselineVisible;
	private readonly Vector3[][] baselinePositions;
	private readonly Vector2[][] baselineUvs;

	private readonly record struct TrackBinding( ModelFile.AnimationTrack Track, int Node, int Mesh );

	private TrackBinding[] tracks = [];
	private double frame;
	// 00472C2B: 33.333332 milliseconds per frame.
	public const float FramesPerSecond = 30f;

	public ModelFile.AnimationGraph? Clip { get; private set; }
	public bool IsPlaying { get; private set; }
	public bool Loop { get; private set; }
	public float Speed { get; set; } = 1f;

	public float Frame => (float)frame;

	public ulong Revision { get; private set; }
	public Matrix4x4[] WorldMatrices { get; }
	public bool[] Visible { get; }
	public Vector3[][] Positions { get; }
	public bool[] DeformedMeshes { get; }
	public Vector2[][] TexCoords { get; }
	public bool[] AnimatedTextureMeshes { get; }

	public bool HasExternalPathTracks => tracks.Any( t => t.Track.HasExternalPath );

	public ModelAnimationPlayer( ModelFile model )
	{
		this.model = model;
		local = new Matrix4x4[model.Nodes.Count];
		baseline = model.Nodes.Select( n => n.LocalMatrix ).ToArray();
		baselineVisible = model.Nodes.Select( node => node.InitiallyVisible ).ToArray();
		baselinePositions = model.MeshDataList.Select( m => m.Positions.ToArray() ).ToArray();
		baselineUvs = model.MeshDataList.Select( m => m.TexCoords.ToArray() ).ToArray();
		TexCoords = model.MeshDataList.Select( m => m.TexCoords.ToArray() ).ToArray();
		AnimatedTextureMeshes = new bool[TexCoords.Length];
		WorldMatrices = new Matrix4x4[model.Nodes.Count];
		Visible = new bool[model.Nodes.Count];
		Positions = model.MeshDataList.Select( m => m.Positions.ToArray() ).ToArray();
		DeformedMeshes = new bool[Positions.Length];
		var offsets = model.Nodes.Select( ( n, i ) => (n.Offset, i) ).ToDictionary( x => x.Offset, x => x.i );
		parents = model.Nodes.Select( n => n.ParentOffset == 0 ? -1 : offsets[n.ParentOffset] ).ToArray();
		meshNodes = model.MeshDataList.Select( m => offsets[m.NodeOffset] ).ToArray();
		evaluationOrder = CreateEvaluationOrder( parents );
		ResetPose();
		Compose();
		Revision++;
	}

	private static int[] CreateEvaluationOrder( int[] parents )
	{
		var order = new List<int>();
		var visited = new bool[parents.Length];
		void Visit( int i )
		{
			if ( visited[i] )
			{
				return;
			}

			if ( parents[i] >= 0 )
			{
				Visit( parents[i] );
			}

			visited[i] = true;
			order.Add( i );
		}

		for ( int i = 0; i < parents.Length; i++ )
		{
			Visit( i );
		}

		return order.ToArray();
	}

	public void Play( ModelFile.AnimationGraph clip, bool loop = true )
	{
		if ( clip.ApparentDuration < clip.StartFrame )
		{
			throw new InvalidDataException( "Invalid animation duration." );
		}

		var bindings = clip.Tracks.Select( BindTrack ).ToArray();
		// Channels absent from the next clip retain the preceding pose, as the
		// original in-place player does (e.g. debris hidden by a create clip).
		CaptureBaseline();
		tracks = bindings;
		Clip = clip;
		Loop = loop;
		IsPlaying = true;
		foreach ( var t in tracks )
		{
			if ( t.Track.Morph != null )
			{
				DeformedMeshes[t.Mesh] = true;
			}
		}

		foreach ( var t in tracks )
		{
			if ( t.Track.UvChannels.Length > 0 )
			{
				AnimatedTextureMeshes[t.Mesh] = true;
			}
		}

		Seek( clip.StartFrame );
	}

	private void CaptureBaseline()
	{
		local.CopyTo( baseline, 0 );
		Visible.CopyTo( baselineVisible, 0 );
		for ( int i = 0; i < Positions.Length; i++ )
		{
			Positions[i].CopyTo( baselinePositions[i], 0 );
		}

		for ( int i = 0; i < TexCoords.Length; i++ )
		{
			TexCoords[i].CopyTo( baselineUvs[i], 0 );
		}
	}

	private TrackBinding BindTrack( ModelFile.AnimationTrack track )
	{
		int node = model.Nodes.FindIndex( n => n.Id == track.TargetNodeId );
		if ( node < 0 )
		{
			throw new InvalidDataException( $"Animation targets missing node {track.TargetNodeId}." );
		}

		int mesh = Array.IndexOf( meshNodes, node );
		if ( track.UvChannels.Length > 0 && (mesh < 0 || track.UvChannels.Length > TexCoords[mesh].Length) )
		{
			throw new InvalidDataException( "UV animation exceeds its target corner count." );
		}

		if ( track.Morph is { } morph )
		{
			if ( mesh < 0 )
			{
				throw new InvalidDataException( "Morph track targets a transform-only node." );
			}

			int count = Positions[mesh].Length;
			bool invalidBlockIndex = morph.Blocks.Any( block => block.PositionIndices.Any( index => index >= count ) );
			bool invalidPositionMap = HasInvalidPositionMap( morph, count );
			if ( invalidBlockIndex || invalidPositionMap )
			{
				throw new InvalidDataException( "Morph position index exceeds its target mesh." );
			}
		}

		return new TrackBinding( track, node, mesh );
	}

	private static bool HasInvalidPositionMap( ModelFile.MorphAnimation morph, int positionCount )
	{
		if ( morph.PositionsPerFrame <= 0 )
		{
			return false;
		}

		if ( morph.PositionMap.Length == 0 )
		{
			return positionCount > morph.PositionsPerFrame;
		}

		return positionCount > morph.PositionMap.Length || morph.PositionMap.Any( index => index >= morph.PositionsPerFrame );
	}

	public void Pause() => IsPlaying = false;

	public void Resume()
	{
		if ( Clip != null )
		{
			IsPlaying = true;
		}
	}

	public void Stop()
	{
		IsPlaying = false;
		Clip = null;
		tracks = [];
		frame = 0;
		Array.Clear( DeformedMeshes );
		Array.Clear( AnimatedTextureMeshes );
		for ( int i = 0; i < baseline.Length; i++ )
		{
			baseline[i] = model.Nodes[i].LocalMatrix;
			baselineVisible[i] = model.Nodes[i].InitiallyVisible;
		}

		for ( int i = 0; i < Positions.Length; i++ )
		{
			model.MeshDataList[i].Positions.CopyTo( baselinePositions[i], 0 );
		}

		for ( int i = 0; i < TexCoords.Length; i++ )
		{
			model.MeshDataList[i].TexCoords.CopyTo( baselineUvs[i], 0 );
		}

		ResetPose();
		Compose();
		Revision++;
	}

	public void Advance( float seconds )
	{
		if ( !float.IsFinite( seconds ) || seconds < 0 || !float.IsFinite( Speed ) || Speed < 0 )
		{
			throw new ArgumentOutOfRangeException( nameof( seconds ), "Animation delta and speed must be finite and nonnegative." );
		}

		if ( !IsPlaying || Clip == null )
		{
			return;
		}

		double next = frame + seconds * (double)FramesPerSecond * Speed;
		double duration = Clip.ApparentDuration - Clip.StartFrame;
		if ( Loop && duration > 0 )
		{
			next = Clip.StartFrame + (next - Clip.StartFrame) % duration;
		}
		else if ( next >= Clip.ApparentDuration )
		{
			next = Clip.ApparentDuration;
			IsPlaying = false;
		}

		Seek( (float)next );
	}

	/// <summary>Sample an absolute source frame. Seeking clamps; only Advance wraps loops.</summary>
	public void Seek( float sourceFrame )
	{
		if ( !float.IsFinite( sourceFrame ) )
		{
			throw new ArgumentOutOfRangeException( nameof( sourceFrame ) );
		}

		if ( Clip == null )
		{
			return;
		}

		frame = Math.Clamp( sourceFrame, Clip.StartFrame, Clip.ApparentDuration );
		ResetPose();
		float time = (float)frame;
		foreach ( var (track, node, mesh) in tracks )
		{
			foreach ( short key in track.Visibility )
			{
				if ( Math.Abs( (int)key ) > time )
				{
					break;
				}

				Visible[node] = key > 0;
			}

			local[node] = SampleTransform( track, time, local[node] );
			if ( track.Morph is { } morph )
			{
				SampleMorph( morph, time, Positions[mesh] );
			}

			for ( int i = 0; i < track.UvChannels.Length; i++ )
			{
				var uv = track.UvChannels[i];
				var (previous, next, fraction) = FindSegment( uv.Times, time, static k => k );
				if ( previous >= 0 )
				{
					TexCoords[mesh][i] = uv.Values[previous] * (1 - fraction) + uv.Values[next] * fraction;
				}
			}
		}

		Compose();
		Revision++;
	}

	private void ResetPose()
	{
		for ( int i = 0; i < local.Length; i++ )
		{
			local[i] = baseline[i];
			Visible[i] = baselineVisible[i];
		}

		for ( int i = 0; i < Positions.Length; i++ )
		{
			baselinePositions[i].CopyTo( Positions[i], 0 );
		}

		for ( int i = 0; i < TexCoords.Length; i++ )
		{
			baselineUvs[i].CopyTo( TexCoords[i], 0 );
		}
	}

	private void Compose()
	{
		foreach ( int i in evaluationOrder )
		{
			WorldMatrices[i] = parents[i] < 0 ? local[i] : local[i] * WorldMatrices[parents[i]];
		}
	}
}
