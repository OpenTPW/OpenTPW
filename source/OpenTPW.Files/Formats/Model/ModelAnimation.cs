using System.Numerics;
using System.Text;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace OpenTPW;

/// <summary>
/// An MD2 animation ("&lt;model&gt;&lt;slot letter&gt;[n].md2"): a bare MD2 header whose pointer at
/// 0x98 leads to tracks that animate the nodes of the base model, numbered as in
/// <see cref="ModelFile.Nodes"/>.
/// </summary>
public class ModelAnimation : BaseFormat
{
	/// <summary>
	/// Frames per second at playback speed 1: 0x470f20 uses (now - start) ms x speed x 0.03.
	/// </summary>
	public const float FramesPerSecond = 30f;

	/// <summary>Segment hiding is an instance binding choice (TP.exe 0x471030), separate from visibility tracks.</summary>
	public enum SegmentHiding { Disabled, NodeIndex, NodeId }

	[Flags]
	public enum TrackFlags : uint
	{
		Path = 0x1,
		Rotation = 0x8,
		Scale = 0x80,
		FollowPath = 0x400,
		Vertex = 0x1000,
		VertexFrames = 0x4000,
		TexCoord = 0x10000,
		Visibility = 0x20000
	}

	public struct RotationKey
	{
		public ushort Frame;

		/// <summary>
		/// Index into <see cref="Track.Eases"/>, or 0xFFFF for linear.
		/// </summary>
		public ushort Ease;
		public Quaternion Rotation;
	}

	public struct ScaleKey
	{
		public ushort Frame;
		public NVector3 Scale;
	}

	public class PathData
	{
		/// <summary>
		/// Bit 1: cubic Bezier with three points per key; bit 3: linear; otherwise Catmull-Rom.
		/// </summary>
		public uint Flags;
		public NVector3[] Points = [];
		public ushort[] Frames = [];
	}

	/// <summary>
	/// Raw vertices that share key frames. Each value packs three signed 10-bit offsets.
	/// </summary>
	public class VertexGroup
	{
		public ushort[] Vertices = [];
		public ushort[] Frames = [];
		public uint[] Values = [];
	}

	public class PackedVertexKeys
	{
		/// <summary>
		/// Bit 1: group 1 is a static pose, applied before the animated groups.
		/// </summary>
		public byte Flags;
		public NVector3 Base;
		public NVector3 Scale;

		/// <summary>
		/// Group 0 animates the bounding box; the rest move vertices.
		/// </summary>
		public VertexGroup[] Groups = [];
	}

	/// <summary>
	/// One stored position per vertex for every frame.
	/// </summary>
	public class VertexFrameKeys
	{
		/// <summary>
		/// For each raw vertex, the stored position it takes; null when they correspond one to one.
		/// </summary>
		public uint[]? Sources;
		public int Count;
		public NVector3[] Positions = [];
	}

	/// <summary>
	/// Key frames for each texture coordinate of the mesh (in <see cref="ModelFile.Mesh.TexCoords"/> order).
	/// </summary>
	public class TexCoordKeys
	{
		public ushort[] Starts = [];
		public ushort[] Counts = [];
		public ushort[] Frames = [];
		public NVector2[] Values = [];
	}

	public class Track
	{
		public TrackFlags Flags;
		public int Node;
		public uint FirstFrame;
		public uint LastFrame;
		public RotationKey[] Rotations = [];

		/// <summary>
		/// Ease curves: eight samples between 0 and 1, at ninths.
		/// </summary>
		public byte[][] Eases = [];
		public ScaleKey[] Scales = [];
		public PathData? Path;

		/// <summary>
		/// The absolute value is a frame; positive shows the node from then on, zero or negative hides it.
		/// </summary>
		public short[] Visibility = [];
		public PackedVertexKeys? PackedVertices;
		public VertexFrameKeys? VertexFrames;
		public TexCoordKeys? TexCoords;
	}

	public uint Flags { get; private set; }
	public uint StartFrame { get; private set; }
	public uint EndFrame { get; private set; }
	public List<Track> Tracks { get; private set; } = [];
	/// <summary>Nodes hidden for this segment, independently of per-frame visibility tracks (TP.exe 0x471030).</summary>
	public ushort[] HiddenNodes { get; private set; } = [];

	public float Length => EndFrame - StartFrame;

	public ModelAnimation( Stream stream )
	{
		ReadFromStream( stream );
	}

	public ModelAnimation( string path )
	{
		ReadFromFile( path );
	}

	private const int TrackSize = 0x40;

	protected override void ReadFromStream( Stream stream )
	{
		using var reader = new BinaryReader( stream, Encoding.ASCII, true );
		Tracks = new();

		reader.BaseStream.Seek( 0x98, SeekOrigin.Begin );
		uint block = reader.ReadUInt32();
		if ( block == 0 )
			return;

		reader.BaseStream.Seek( block, SeekOrigin.Begin );
		Flags = reader.ReadUInt32();
		StartFrame = reader.ReadUInt32();
		EndFrame = reader.ReadUInt32();

		reader.BaseStream.Seek( block + 0x12, SeekOrigin.Begin );
		ushort trackCount = reader.ReadUInt16();
		reader.BaseStream.Seek( block + 0x2C, SeekOrigin.Begin );
		uint trackTable = reader.ReadUInt32();
		reader.BaseStream.Seek( block + 0x1A, SeekOrigin.Begin );
		ushort hiddenCount = reader.ReadUInt16();
		reader.BaseStream.Seek( block + 0x38, SeekOrigin.Begin );
		uint hiddenTable = reader.ReadUInt32();
		if ( hiddenCount != 0 && hiddenTable == 0 ) throw new InvalidDataException( "Animation hidden-node table is missing" );
		if ( hiddenCount != 0 && (hiddenTable > stream.Length || hiddenCount * 2L > stream.Length - hiddenTable) )
			throw new InvalidDataException( "Animation hidden-node table is truncated" );
		reader.BaseStream.Seek( hiddenTable, SeekOrigin.Begin );
		HiddenNodes = new ushort[hiddenCount];
		for ( int i = 0; i < hiddenCount; i++ ) HiddenNodes[i] = reader.ReadUInt16();

		for ( int i = 0; i < trackCount; i++ )
			Tracks.Add( ReadTrack( reader, trackTable + (uint)(i * TrackSize) ) );
	}

	private static Track ReadTrack( BinaryReader reader, uint offset )
	{
		reader.BaseStream.Seek( offset + 4, SeekOrigin.Begin );

		var track = new Track
		{
			Flags = (TrackFlags)reader.ReadUInt32(),
			FirstFrame = reader.ReadUInt32(),
			LastFrame = reader.ReadUInt32()
		};

		ushort rotationCount = reader.ReadUInt16();
		ushort scaleCount = reader.ReadUInt16();
		track.Node = reader.ReadUInt16();
		ushort visibilityCount = reader.ReadUInt16();
		uint pathOffset = reader.ReadUInt32();
		uint rotationOffset = reader.ReadUInt32();
		uint scaleOffset = reader.ReadUInt32();
		_ = reader.ReadUInt32(); // Route data for flag 0x200 (buses following the level's road)
		uint vertexOffset = reader.ReadUInt32();
		uint texCoordOffset = reader.ReadUInt32();
		uint visibilityOffset = reader.ReadUInt32();
		uint easeOffset = reader.ReadUInt32();

		if ( track.Flags.HasFlag( TrackFlags.Rotation ) )
		{
			track.Rotations = new RotationKey[rotationCount];
			int easeCount = 0;

			for ( int i = 0; i < rotationCount; i++ )
			{
				reader.BaseStream.Seek( rotationOffset + i * 20, SeekOrigin.Begin );
				var key = new RotationKey
				{
					Frame = reader.ReadUInt16(),
					Ease = reader.ReadUInt16(),
					Rotation = new Quaternion( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() )
				};

				if ( key.Ease != 0xFFFF )
					easeCount = Math.Max( easeCount, key.Ease + 1 );

				track.Rotations[i] = key;
			}

			if ( easeOffset != 0 )
			{
				reader.BaseStream.Seek( easeOffset, SeekOrigin.Begin );
				track.Eases = new byte[easeCount][];
				for ( int i = 0; i < easeCount; i++ )
					track.Eases[i] = reader.ReadBytes( 8 );
			}
		}

		if ( track.Flags.HasFlag( TrackFlags.Scale ) )
		{
			track.Scales = new ScaleKey[scaleCount];
			for ( int i = 0; i < scaleCount; i++ )
			{
				reader.BaseStream.Seek( scaleOffset + i * 16, SeekOrigin.Begin );
				ushort frame = reader.ReadUInt16();
				_ = reader.ReadUInt16();
				track.Scales[i] = new ScaleKey { Frame = frame, Scale = ReadVector3( reader ) };
			}
		}

		if ( track.Flags.HasFlag( TrackFlags.Path ) && pathOffset != 0 )
			track.Path = ReadPath( reader, pathOffset );

		if ( track.Flags.HasFlag( TrackFlags.Visibility ) )
		{
			reader.BaseStream.Seek( visibilityOffset, SeekOrigin.Begin );
			track.Visibility = new short[visibilityCount];
			for ( int i = 0; i < visibilityCount; i++ )
				track.Visibility[i] = reader.ReadInt16();
		}

		if ( track.Flags.HasFlag( TrackFlags.Vertex ) && vertexOffset != 0 )
		{
			if ( track.Flags.HasFlag( TrackFlags.VertexFrames ) )
				track.VertexFrames = ReadVertexFrames( reader, vertexOffset, track.LastFrame + 1 );
			else
				track.PackedVertices = ReadPackedVertices( reader, vertexOffset );
		}

		if ( track.Flags.HasFlag( TrackFlags.TexCoord ) && texCoordOffset != 0 )
			track.TexCoords = ReadTexCoords( reader, texCoordOffset );

		return track;
	}

	private static NVector3 ReadVector3( BinaryReader reader ) => new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );

	private static ushort[] ReadUInt16s( BinaryReader reader, uint offset, int count, int stride = 2 )
	{
		var values = new ushort[count];
		for ( int i = 0; i < count; i++ )
		{
			reader.BaseStream.Seek( offset + i * stride, SeekOrigin.Begin );
			values[i] = reader.ReadUInt16();
		}

		return values;
	}

	private static PathData? ReadPath( BinaryReader reader, uint offset )
	{
		reader.BaseStream.Seek( offset, SeekOrigin.Begin );
		var path = new PathData { Flags = reader.ReadUInt32() };
		ushort pointCount = reader.ReadUInt16();
		ushort keyCount = reader.ReadUInt16();
		uint pointOffset = reader.ReadUInt32();
		uint keyOffset = reader.ReadUInt32();

		reader.BaseStream.Seek( pointOffset, SeekOrigin.Begin );
		path.Points = new NVector3[pointCount];
		for ( int i = 0; i < pointCount; i++ )
			path.Points[i] = ReadVector3( reader );

		path.Frames = ReadUInt16s( reader, keyOffset, keyCount, 4 );
		return path.Points.Length > 0 ? path : null;
	}

	private static PackedVertexKeys ReadPackedVertices( BinaryReader reader, uint offset )
	{
		reader.BaseStream.Seek( offset, SeekOrigin.Begin );
		var keys = new PackedVertexKeys { Flags = reader.ReadByte() };
		_ = reader.ReadByte();
		ushort groupCount = reader.ReadUInt16();
		reader.BaseStream.Seek( 8, SeekOrigin.Current );
		uint groupOffset = reader.ReadUInt32();
		_ = reader.ReadUInt32(); // Used at run time
		keys.Base = ReadVector3( reader );
		keys.Scale = ReadVector3( reader );

		keys.Groups = new VertexGroup[groupCount];
		for ( int i = 0; i < groupCount; i++ )
		{
			reader.BaseStream.Seek( groupOffset + i * 20, SeekOrigin.Begin );
			ushort keyCount = reader.ReadUInt16();
			ushort vertexCount = reader.ReadUInt16();
			uint vertexOffset = reader.ReadUInt32();
			uint frameOffset = reader.ReadUInt32();
			uint valueOffset = reader.ReadUInt32();

			var group = new VertexGroup
			{
				Vertices = ReadUInt16s( reader, vertexOffset, vertexCount ),
				Frames = ReadUInt16s( reader, frameOffset, keyCount ),
				Values = new uint[keyCount * vertexCount]
			};

			reader.BaseStream.Seek( valueOffset, SeekOrigin.Begin );
			for ( int j = 0; j < group.Values.Length; j++ )
				group.Values[j] = reader.ReadUInt32();

			keys.Groups[i] = group;
		}

		return keys;
	}

	private static VertexFrameKeys ReadVertexFrames( BinaryReader reader, uint offset, uint frameCount )
	{
		reader.BaseStream.Seek( offset, SeekOrigin.Begin );
		uint sourceOffset = reader.ReadUInt32();
		var keys = new VertexFrameKeys { Count = (int)reader.ReadUInt32() };
		uint positionOffset = reader.ReadUInt32();

		if ( sourceOffset != 0 )
		{
			reader.BaseStream.Seek( sourceOffset, SeekOrigin.Begin );
			keys.Sources = new uint[keys.Count];
			for ( int i = 0; i < keys.Count; i++ )
				keys.Sources[i] = reader.ReadUInt32();
		}

		// One key per frame; don't read past the end of the file
		long available = (reader.BaseStream.Length - positionOffset) / 12;
		long positionCount = Math.Min( (long)keys.Count * frameCount, available );

		reader.BaseStream.Seek( positionOffset, SeekOrigin.Begin );
		keys.Positions = new NVector3[positionCount];
		for ( long i = 0; i < positionCount; i++ )
			keys.Positions[i] = ReadVector3( reader );

		return keys;
	}

	private static TexCoordKeys ReadTexCoords( BinaryReader reader, uint offset )
	{
		reader.BaseStream.Seek( offset, SeekOrigin.Begin );
		uint count = reader.ReadUInt32();
		uint rangeOffset = reader.ReadUInt32();
		uint keyCount = reader.ReadUInt32();
		uint frameOffset = reader.ReadUInt32();
		uint valueOffset = reader.ReadUInt32();

		var keys = new TexCoordKeys { Starts = new ushort[count], Counts = new ushort[count] };
		reader.BaseStream.Seek( rangeOffset, SeekOrigin.Begin );
		for ( int i = 0; i < count; i++ )
		{
			keys.Starts[i] = reader.ReadUInt16();
			keys.Counts[i] = reader.ReadUInt16();
		}

		keys.Frames = ReadUInt16s( reader, frameOffset, (int)keyCount );
		keys.Values = new NVector2[keyCount];
		reader.BaseStream.Seek( valueOffset, SeekOrigin.Begin );
		for ( int i = 0; i < keyCount; i++ )
			keys.Values[i] = new NVector2( reader.ReadSingle(), reader.ReadSingle() );

		return keys;
	}

	/// <summary>
	/// Poses <paramref name="model"/> at <paramref name="frame"/>. Tracks apply visibility, vertices,
	/// rotation, scale and then translation, as the game does.
	/// </summary>
	public ModelPose Evaluate( ModelFile model, float frame, SegmentHiding segmentHiding = SegmentHiding.Disabled )
	{
		var pose = new ModelPose( model );
		if ( segmentHiding != SegmentHiding.Disabled )
		foreach ( ushort reference in HiddenNodes )
		{
			int node = segmentHiding == SegmentHiding.NodeId ? model.Nodes.FindIndex( candidate => candidate.Id == reference ) : reference;
			// ID binding searches the instantiated model and leaves unmatched IDs alone.
			if ( node < 0 ) continue;
			if ( node >= model.Nodes.Count ) throw new InvalidDataException( $"Animation hides nonexistent node {reference}" );
			if ( (model.Nodes[node].Flags & 0x80000000) == 0 ) pose.Hidden[node] = true;
		}
		var raw = new NVector3[model.Nodes.Count][];

		foreach ( var track in Tracks )
		{
			if ( track.Node < 0 || track.Node >= model.Nodes.Count )
				continue;

			var mesh = model.Nodes[track.Node].Mesh;
			ref var local = ref pose.LocalTransforms[track.Node];

			// The last entry whose frame has been reached decides
			for ( int i = track.Visibility.Length - 1; i >= 0; i-- )
			{
				if ( Math.Abs( (int)track.Visibility[i] ) <= frame )
				{
					pose.Hidden[track.Node] = track.Visibility[i] < 1;
					break;
				}
			}

			if ( mesh != null && (track.PackedVertices != null || track.VertexFrames != null) )
			{
				raw[track.Node] ??= mesh.RawPositions.Select( p => p.GetSystemVector3() ).ToArray();

				if ( track.PackedVertices is { } packed )
					ApplyPackedVertices( packed, frame, raw[track.Node] );
				else if ( track.VertexFrames is { } vertexFrames )
					ApplyVertexFrames( vertexFrames, frame, raw[track.Node] );
			}

			if ( FindSegment( track.Rotations.Length, i => track.Rotations[i].Frame, frame, out int key, out int next, out float t ) )
			{
				var a = track.Rotations[key];
				if ( a.Ease < track.Eases.Length )
					t = Ease( track.Eases[a.Ease], t );

				SetRotation( ref local, Slerp( a.Rotation, track.Rotations[next].Rotation, t ) );
			}

			if ( FindSegment( track.Scales.Length, i => track.Scales[i].Frame, frame, out key, out next, out t ) )
				SetScale( ref local, NVector3.Lerp( track.Scales[key].Scale, track.Scales[next].Scale, t ) );

			var path = track.Path;
			if ( path != null && FindSegment( path.Frames.Length, i => path.Frames[i], frame, out key, out _, out t ) )
			{
				int index = (path.Flags & 2) != 0 ? key * 3 + 1 : key;
				var position = PathPoint( path, index, t );
				local.M41 = position.X;
				local.M42 = position.Y;
				local.M43 = position.Z;

				if ( track.Flags.HasFlag( TrackFlags.FollowPath ) )
				{
					// The game differentiates the curve; a small step gives the same direction
					var forward = NVector3.Normalize( PathPoint( path, index, t + 0.01f ) - position );
					var up = track.Flags.HasFlag( TrackFlags.Rotation ) ? new NVector3( local.M21, local.M22, local.M23 ) : NVector3.UnitY;
					var side = NVector3.Normalize( NVector3.Cross( up, forward ) );
					up = NVector3.Cross( forward, side );

					local.M11 = side.X; local.M12 = side.Y; local.M13 = side.Z;
					local.M21 = up.X; local.M22 = up.Y; local.M23 = up.Z;
					local.M31 = forward.X; local.M32 = forward.Y; local.M33 = forward.Z;
				}
			}

			if ( mesh != null && track.TexCoords != null )
				pose.TexCoords[track.Node] = EvaluateTexCoords( track.TexCoords, frame, mesh.TexCoords.Length );
		}

		for ( int i = 0; i < raw.Length; i++ )
		{
			// Raw positions are only created for nodes that carry a mesh.
			if ( raw[i] == null || model.Nodes[i].Mesh is not { } nodeMesh )
				continue;

			var order = nodeMesh.VertexOrder;
			var positions = new Vector3[order.Length];
			for ( int j = 0; j < order.Length; j++ )
				positions[j] = order[j] < raw[i].Length ? raw[i][order[j]] : NVector3.Zero;

			pose.Positions[i] = positions;
		}

		pose.ResolveWorldTransforms( model );
		return pose;
	}

	/// <summary>
	/// The last key at or before <paramref name="frame"/>, the key after it (the same key at the end)
	/// and the fraction between them. False before the first key, where the track has no effect.
	/// </summary>
	private static bool FindSegment( int count, Func<int, ushort> frameOf, float frame, out int key, out int next, out float t )
	{
		key = next = 0;
		t = 0;

		if ( count == 0 || frame < frameOf( 0 ) )
			return false;

		while ( key + 1 < count && frameOf( key + 1 ) <= frame )
			key++;

		next = Math.Min( key + 1, count - 1 );
		float t0 = frameOf( key ), t1 = frameOf( next );
		t = t1 > t0 ? Math.Clamp( (frame - t0) / (t1 - t0), 0f, 1f ) : 0f;
		return true;
	}

	private static float Ease( byte[] curve, float t )
	{
		float s = t * 8.99999f;
		int i = Math.Clamp( (int)s, 0, 8 );
		float f = s - i;
		float lo = i == 0 ? 0f : curve[i - 1] / 255f;
		float hi = i == 8 ? 1f : curve[i] / 255f;
		return lo * (1 - f) + hi * f;
	}

	/// <summary>
	/// The game's slerp: it does not flip to the shorter arc.
	/// </summary>
	private static Quaternion Slerp( Quaternion a, Quaternion b, float t )
	{
		float dot = Quaternion.Dot( a, b );

		if ( dot + 1 > 0.001f )
		{
			float wa = 1 - t, wb = t;
			if ( 1 - dot > 0.001f )
			{
				float theta = MathF.Acos( Math.Clamp( dot, -1f, 1f ) );
				float sin = MathF.Sin( theta );
				wa = MathF.Sin( (1 - t) * theta ) / sin;
				wb = MathF.Sin( t * theta ) / sin;
			}

			return a * wa + b * wb;
		}

		// Opposite rotations: go through a perpendicular one
		var p = new Quaternion( -a.Y, a.X, -a.W, a.Z );
		float pa = MathF.Sin( (1 - t) * MathF.PI / 2 ), pb = MathF.Sin( t * MathF.PI / 2 );
		return new Quaternion( pa * a.X + pb * p.X, pa * a.Y + pb * p.Y, pa * a.Z + pb * p.Z, p.W );
	}

	/// <summary>
	/// Replaces the 3x3 part of <paramref name="m"/>, keeping its translation.
	/// </summary>
	private static void SetRotation( ref Matrix4x4 m, Quaternion q )
	{
		var r = q.LengthSquared() > 0 ? Matrix4x4.CreateFromQuaternion( Quaternion.Normalize( q ) ) : Matrix4x4.Identity;
		m.M11 = r.M11; m.M12 = r.M12; m.M13 = r.M13;
		m.M21 = r.M21; m.M22 = r.M22; m.M23 = r.M23;
		m.M31 = r.M31; m.M32 = r.M32; m.M33 = r.M33;
	}

	/// <summary>
	/// Sets the lengths of the first three rows.
	/// </summary>
	private static void SetScale( ref Matrix4x4 m, NVector3 scale )
	{
		static (float, float, float) Row( float x, float y, float z, float s )
		{
			float length = MathF.Sqrt( x * x + y * y + z * z );
			return length > 0 ? (x * s / length, y * s / length, z * s / length) : (x, y, z);
		}

		(m.M11, m.M12, m.M13) = Row( m.M11, m.M12, m.M13, scale.X );
		(m.M21, m.M22, m.M23) = Row( m.M21, m.M22, m.M23, scale.Y );
		(m.M31, m.M32, m.M33) = Row( m.M31, m.M32, m.M33, scale.Z );
	}

	/// <summary>
	/// A point on <paramref name="path"/>; <paramref name="index"/> is the key's first point. Points wrap
	/// around except on linear paths.
	/// </summary>
	private static NVector3 PathPoint( PathData path, int index, float t )
	{
		int n = path.Points.Length;
		int whole = (int)MathF.Floor( t );
		t -= whole;

		if ( (path.Flags & 8) != 0 )
		{
			int i = Math.Min( index + whole, n - 1 );
			int j = Math.Min( i + 1, n - 1 );
			return NVector3.Lerp( path.Points[i], path.Points[j], t );
		}

		bool bezier = (path.Flags & 2) != 0;
		int b = n + index + whole * (bezier ? 3 : 1);
		var p0 = path.Points[(b - 1) % n];
		var p1 = path.Points[b % n];
		var p2 = path.Points[(b + 1) % n];
		var p3 = path.Points[(b + 2) % n];

		if ( bezier )
			return p0 + t * ((p1 * 3 - p0 * 3) + t * (((p0 + p2) * 3 - p1 * 6) + t * ((p1 * 3 - (p2 * 3 + p0)) + p3)));

		return 0.5f * (p1 * 2 + t * ((p2 - p0) + t * ((p0 * 2 - p1 * 5 + p2 * 4 - p3) + t * (p1 * 3 - p2 * 3 - p0 + p3))));
	}

	private static NVector3 Unpack( uint value, PackedVertexKeys keys )
	{
		static int Signed10( uint v, int shift ) => (int)(v << (22 - shift)) >> 22;
		return new NVector3( Signed10( value, 0 ), Signed10( value, 10 ), Signed10( value, 20 ) ) * keys.Scale + keys.Base;
	}

	private static void ApplyPackedVertices( PackedVertexKeys keys, float frame, NVector3[] raw )
	{
		void Put( int i, NVector3 p )
		{
			if ( i < raw.Length )
				raw[i] = p;
		}

		int first = 1;
		if ( (keys.Flags & 2) != 0 && keys.Groups.Length > 1 )
		{
			var pose = keys.Groups[1];
			for ( int c = 0; c < pose.Vertices.Length && c < pose.Values.Length; c++ )
				Put( pose.Vertices[c], Unpack( pose.Values[c], keys ) );

			first = 2;
		}

		for ( int g = first; g < keys.Groups.Length; g++ )
		{
			var group = keys.Groups[g];
			int vertexCount = group.Vertices.Length, keyCount = group.Frames.Length;
			if ( keyCount == 0 )
				continue;

			// The game advances while the next key's frame is before the current frame
			int key = 0;
			while ( key + 2 < keyCount && group.Frames[key + 1] < frame )
				key++;

			int next = Math.Min( key + 1, keyCount - 1 );
			float t0 = group.Frames[key], t1 = group.Frames[next];
			float t = t1 > t0 ? Math.Clamp( (frame - t0) / (t1 - t0), 0f, 1f ) : 0f;

			for ( int c = 0; c < vertexCount; c++ )
			{
				var a = Unpack( group.Values[key * vertexCount + c], keys );
				var b = Unpack( group.Values[next * vertexCount + c], keys );
				Put( group.Vertices[c], NVector3.Lerp( a, b, t ) );
			}
		}
	}

	/// <summary>
	/// The game takes the key of the current whole frame without blending.
	/// </summary>
	private static void ApplyVertexFrames( VertexFrameKeys keys, float frame, NVector3[] raw )
	{
		if ( keys.Count == 0 || keys.Positions.Length < keys.Count )
			return;

		int frameCount = keys.Positions.Length / keys.Count;
		int start = Math.Clamp( (int)frame, 0, frameCount - 1 ) * keys.Count;

		for ( int i = 0; i < raw.Length; i++ )
		{
			long source = keys.Sources != null ? (i < keys.Sources.Length ? keys.Sources[i] : -1) : i;
			if ( source >= 0 && source < keys.Count )
				raw[i] = keys.Positions[start + source];
		}
	}

	private static Vector2[] EvaluateTexCoords( TexCoordKeys keys, float frame, int texCoordCount )
	{
		var result = new Vector2[texCoordCount];
		int whole = (int)frame;

		for ( int i = 0; i < texCoordCount && i < keys.Starts.Length; i++ )
		{
			int start = keys.Starts[i], count = keys.Counts[i];
			if ( count == 0 || start + count > keys.Values.Length )
				continue;

			// First key after the current whole frame; past the last key, hold the last value
			int k = 1;
			while ( k < count && keys.Frames[start + k] <= whole )
				k++;

			if ( k >= count || keys.Frames[start] > whole )
			{
				result[i] = keys.Values[start + (k >= count ? count - 1 : 0)];
				continue;
			}

			float f0 = keys.Frames[start + k - 1], f1 = keys.Frames[start + k];
			float t = f1 > f0 ? (frame - f0) / (f1 - f0) : 0f;
			result[i] = NVector2.Lerp( keys.Values[start + k - 1], keys.Values[start + k], t );
		}

		return result;
	}
}

/// <summary>
/// A model posed by <see cref="ModelAnimation.Evaluate"/>. Arrays are indexed by node.
/// </summary>
public class ModelPose
{
	/// <summary>
	/// Transform relative to the parent node; the rest pose where no track moves the node.
	/// </summary>
	public Matrix4x4[] LocalTransforms { get; }

	/// <summary>
	/// Transform to model space: the local transform times the parent's world transform.
	/// </summary>
	public Matrix4x4[] WorldTransforms { get; }
	public bool[] Hidden { get; }

	/// <summary>
	/// Animated positions in <see cref="ModelFile.Mesh.Vertices"/> order, or null where unchanged.
	/// </summary>
	public Vector3[][] Positions { get; }

	/// <summary>
	/// Animated texture coordinates in <see cref="ModelFile.Mesh.TexCoords"/> order, or null where unchanged.
	/// </summary>
	public Vector2[][] TexCoords { get; }

	public ModelPose( ModelFile model )
	{
		int count = model.Nodes.Count;
		LocalTransforms = model.Nodes.Select( n => n.LocalTransform ).ToArray();
		WorldTransforms = model.Nodes.Select( n => n.WorldTransform ).ToArray();
		Hidden = new bool[count];
		Positions = new Vector3[count][];
		TexCoords = new Vector2[count][];
	}

	internal void ResolveWorldTransforms( ModelFile model )
	{
		var resolved = new bool[LocalTransforms.Length];

		Matrix4x4 Resolve( int node, int depth )
		{
			if ( resolved[node] )
				return WorldTransforms[node];

			var world = LocalTransforms[node];
			int parent = model.Nodes[node].ParentIndex;
			if ( parent >= 0 && depth < LocalTransforms.Length )
				world *= Resolve( parent, depth + 1 );

			WorldTransforms[node] = world;
			resolved[node] = true;
			return world;
		}

		for ( int i = 0; i < LocalTransforms.Length; i++ )
			Resolve( i, 0 );
	}
}
