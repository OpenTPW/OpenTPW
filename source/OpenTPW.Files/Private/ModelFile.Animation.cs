using System.Numerics;

namespace OpenTPW;

public partial class ModelFile
{
	// Sizes and flags below describe the original MD2 storage, not tuning values.
	private const uint AnimationGraphBytes = 0x40;
	private const uint AnimationTrackBytes = 0x40;
	private const uint RotationKeyBytes = 20;
	private const uint ScaleKeyBytes = 16;
	private const uint Vector3Bytes = 3 * sizeof( float );
	public const int RotationEasingSamples = 8;

	[Flags]
	public enum AnimationChannels : uint
	{
		Translation = 0x1,
		Rotation = 0x8,
		Scale = 0x80,
		ExternalPath = 0x600,
		Morph = 0x1000,
		FloatMorph = 0x4000,
		UvAnimation = 0x10000,
		Visibility = 0x20000
	}

	public class AnimationGraph
	{
		public uint Flags { get; init; }
		public uint StartFrame { get; init; }
		public uint Offset { get; init; }
		public uint ApparentDuration { get; init; }
		public ushort TrackCount { get; init; }
		public uint TrackTableOffset { get; init; }
		public List<AnimationTrack> Tracks { get; init; } = new();
	}

	public class AnimationTrack
	{
		private bool HasChannel( AnimationChannels channel ) => (Flags & (uint)channel) != 0;

		public bool HasTranslation => HasChannel( AnimationChannels.Translation );

		public bool HasRotation => HasChannel( AnimationChannels.Rotation );

		public bool HasScale => HasChannel( AnimationChannels.Scale );

		public bool HasExternalPath => HasChannel( AnimationChannels.ExternalPath );

		public bool HasMorph => HasChannel( AnimationChannels.Morph );

		public bool HasFloatMorph => HasChannel( AnimationChannels.FloatMorph );

		public bool HasUvAnimation => HasChannel( AnimationChannels.UvAnimation );

		public bool HasVisibility => HasChannel( AnimationChannels.Visibility );

		public uint Index { get; init; }
		public uint Flags { get; init; }
		public uint ApparentDuration { get; init; }
		public uint Target { get; init; }

		// Low ushort: node ID; high ushort: visibility key count.
		public ushort TargetNodeId => (ushort)Target;

		public TranslationCurve? Translation { get; set; }
		public short[] Visibility { get; set; } = Array.Empty<short>();
		public byte[] RotationEasing { get; set; } = Array.Empty<byte>();
		public MorphAnimation? Morph { get; set; }
		public UvChannel[] UvChannels { get; set; } = Array.Empty<UvChannel>();
		public ushort Keys20Count { get; init; }
		public ushort Keys16Count { get; init; }
		public uint Keys20Offset { get; init; }
		public uint Keys16Offset { get; init; }

		// Other fields are relocated by the original loader but not decoded here.
		public uint[] OtherOffsets { get; init; } = Array.Empty<uint>();
		public Key20[] Keys20 { get; init; } = Array.Empty<Key20>();
		public Key16[] Keys16 { get; init; } = Array.Empty<Key16>();
	}

	// These names describe storage, not transform semantics. The playback
	// routine uses 0x14- and 0x10-byte keys in different channel branches.
	public readonly record struct Key20( ushort Time, ushort Metadata, Vector4 Components );

	public readonly record struct Key16( ushort Time, ushort Metadata, Vector3 Components );

	private AnimationGraph ReadAnimation( uint offset )
	{
		if ( U32( 8 ) != 0xCB )
		{
			throw new NotSupportedException( "Unverified MD2 animation graph revision." );
		}

		Require( offset, AnimationGraphBytes, "animation graph" );
		ushort count = U16( offset + 0x12 );
		uint table = U32( offset + 0x2C );
		RequireSection( table, (ulong)count * AnimationTrackBytes, "animation tracks" );
		var graph = new AnimationGraph
		{
			Flags = U32( offset ),
			StartFrame = U32( offset + 4 ),
			Offset = offset,
			ApparentDuration = U32( offset + 8 ),
			TrackCount = count,
			TrackTableOffset = table
		};
		for ( int i = 0; i < count; i++ )
		{
			uint trackOffset = table + (uint)i * AnimationTrackBytes;
			ushort count20 = U16( trackOffset + 0x10 ), count16 = U16( trackOffset + 0x12 );
			uint offset20 = U32( trackOffset + 0x1C ), offset16 = U32( trackOffset + 0x20 );
			// Only these two channel layouts have been identified in playback.
			// Keep every other pointer raw until its channel type is known.
			var track = new AnimationTrack
			{
				Index = U32( trackOffset ),
				Flags = U32( trackOffset + 4 ),
				ApparentDuration = U32( trackOffset + 0x0C ),
				Target = U32( trackOffset + 0x14 ),
				Keys20Count = count20,
				Keys16Count = count16,
				Keys20Offset = offset20,
				Keys16Offset = offset16,
				OtherOffsets = new[] {
					U32( trackOffset + 0x18 ),
					U32( trackOffset + 0x24 ),
					U32( trackOffset + 0x28 ),
					U32( trackOffset + 0x2C ),
					U32( trackOffset + 0x30 ),
					U32( trackOffset + 0x34 )
				},
				Keys20 = ReadKeys20( offset20, count20 ),
				Keys16 = ReadKeys16( offset16, count16 )
			};
			ReadPlaybackChannels( track, trackOffset );
			graph.Tracks.Add( track );
		}

		return graph;
	}

	private Key20[] ReadKeys20( uint offset, ushort count )
	{
		if ( offset == 0 || count == 0 )
		{
			return Array.Empty<Key20>();
		}

		RequireSection( offset, (ulong)count * RotationKeyBytes, "20-byte animation keys" );
		var keys = new Key20[count];
		for ( int i = 0; i < count; i++ )
		{
			uint k = offset + (uint)i * RotationKeyBytes;
			keys[i] = new Key20( U16( k ), U16( k + 2 ), new Vector4( F32( k + 4 ), F32( k + 8 ), F32( k + 0x0C ), F32( k + 0x10 ) ) );
		}

		return keys;
	}

	private Key16[] ReadKeys16( uint offset, ushort count )
	{
		if ( offset == 0 || count == 0 )
		{
			return Array.Empty<Key16>();
		}

		RequireSection( offset, (ulong)count * ScaleKeyBytes, "16-byte animation keys" );
		var keys = new Key16[count];
		for ( int i = 0; i < count; i++ )
		{
			uint k = offset + (uint)i * ScaleKeyBytes;
			keys[i] = new Key16( U16( k ), U16( k + 2 ), new Vector3( F32( k + 4 ), F32( k + 8 ), F32( k + 0x0C ) ) );
		}

		return keys;
	}

	public sealed class UvChannel
	{
		public ushort[] Times { get; init; } = Array.Empty<ushort>();
		public Vector2[] Values { get; init; } = Array.Empty<Vector2>();
	}

	public sealed class TranslationCurve
	{
		private const uint CubicBezierFlag = 2;
		public uint Flags { get; init; }
		public bool IsCubicBezier => (Flags & CubicBezierFlag) != 0;
		public Vector3[] Points { get; init; } = Array.Empty<Vector3>();
		public ushort[] Times { get; init; } = Array.Empty<ushort>();
	}

	public sealed class MorphAnimation
	{
		public Vector3 Offset { get; init; }
		public Vector3 Scale { get; init; }
		public MorphBlock[] Blocks { get; init; } = Array.Empty<MorphBlock>();
		public uint[] PositionMap { get; init; } = Array.Empty<uint>();
		public Vector3[] FloatPositions { get; init; } = Array.Empty<Vector3>();
		public int PositionsPerFrame { get; init; }
	}

	public sealed class MorphBlock
	{
		public ushort[] PositionIndices { get; init; } = Array.Empty<ushort>();
		public ushort[] Times { get; init; } = Array.Empty<ushort>();
		public uint[] PackedPositions { get; init; } = Array.Empty<uint>();
	}

	private Vector3 Vector( uint offset ) => new( F32( offset ), F32( offset + 4 ), F32( offset + 8 ) );

	private ushort[] ReadTimes( uint offset, int count, uint stride )
	{
		RequireSection( offset, (ulong)count * stride, "animation times" );
		var times = new ushort[count];
		for ( int i = 0; i < count; i++ )
		{
			times[i] = U16( offset + (uint)i * stride );
			if ( i > 0 && times[i] < times[i - 1] )
			{
				throw new InvalidDataException( "Animation keys are not sorted." );
			}
		}

		return times;
	}

	private void ReadPlaybackChannels( AnimationTrack track, uint trackOffset )
	{
		// tp_nocd.exe 00470A4C / 00470AB5 binds the low ushort at +14.
		// 00471891 uses the high ushort as a signed visibility-time count.
		if ( track.HasVisibility )
		{
			ReadVisibility( track, trackOffset );
		}

		if ( track.HasTranslation )
		{
			ReadTranslation( track, trackOffset );
		}

		if ( track.HasRotation && track.Keys20.Length > 0 )
		{
			ReadRotationEasing( track, trackOffset );
		}

		if ( track.HasScale )
		{
			ReadTimes( track.Keys16Offset, track.Keys16.Length, ScaleKeyBytes );
		}

		if ( track.HasMorph )
		{
			track.Morph = ReadMorph( track, U32( trackOffset + 0x28 ) );
		}

		if ( track.HasUvAnimation )
		{
			ReadUvChannels( track, trackOffset );
		}
	}

	private void ReadVisibility( AnimationTrack track, uint trackOffset )
	{
		int count = U16( trackOffset + 0x16 );
		uint offset = U32( trackOffset + 0x30 );
		RequireSection( offset, (ulong)count * 2, "visibility keys" );
		track.Visibility = new short[count];
		for ( int i = 0; i < count; i++ )
		{
			track.Visibility[i] = unchecked((short)U16( offset + (uint)i * 2 ));
			if ( i > 0 && Math.Abs( (int)track.Visibility[i] ) < Math.Abs( (int)track.Visibility[i - 1] ) )
			{
				throw new InvalidDataException( "Visibility keys are not sorted." );
			}
		}
	}

	private void ReadTranslation( AnimationTrack track, uint trackOffset )
	{
		uint curve = U32( trackOffset + 0x18 );
		RequireSection( curve, 16, "translation curve" );
		int count = U16( curve + 4 );
		uint pointsOffset = U32( curve + 8 );
		RequireSection( pointsOffset, (ulong)count * Vector3Bytes, "translation points" );
		var points = new Vector3[count];
		for ( int i = 0; i < count; i++ )
		{
			points[i] = Vector( pointsOffset + (uint)i * Vector3Bytes );
		}

		track.Translation = new TranslationCurve
		{
			Flags = U32( curve ),
			Points = points,
			Times = ReadTimes( U32( curve + 12 ), U16( curve + 6 ), 4 )
		};
	}

	private void ReadRotationEasing( AnimationTrack track, uint trackOffset )
	{
		ReadTimes( track.Keys20Offset, track.Keys20.Length, RotationKeyBytes );
		// Metadata selects an eight-byte easing table; FFFF means linear.
		int tableCount = track.Keys20.Where( key => key.Metadata != ushort.MaxValue )
			.Select( key => (int)key.Metadata + 1 ).DefaultIfEmpty( 0 ).Max();
		int count = tableCount * RotationEasingSamples;
		uint offset = U32( trackOffset + 0x34 );
		RequireSection( offset, (ulong)count, "rotation easing" );
		track.RotationEasing = data.AsSpan( (int)offset, count ).ToArray();
	}

	private void ReadUvChannels( AnimationTrack track, uint trackOffset )
	{
		// 004746E2..004747E8: a first-key/count pair for each UV corner.
		uint uv = U32( trackOffset + 0x2C );
		RequireSection( uv, 20, "UV animation header" );
		uint count = U32( uv ), ranges = U32( uv + 4 ), times = U32( uv + 12 ), values = U32( uv + 16 );
		RequireSection( ranges, (ulong)count * 4, "UV animation ranges" );
		track.UvChannels = new UvChannel[count];
		for ( uint i = 0; i < count; i++ )
		{
			uint start = U16( ranges + i * 4 );
			int keys = U16( ranges + i * 4 + 2 );
			RequireSection( values, ((ulong)start + (uint)keys) * 8, "animated UV values" );
			var coordinates = new Vector2[keys];
			for ( int j = 0; j < keys; j++ )
			{
				uint p = values + (start + (uint)j) * 8;
				coordinates[j] = new Vector2( F32( p ), F32( p + 4 ) );
			}

			track.UvChannels[i] = new UvChannel
			{
				Times = ReadTimes( checked(times + start * 2), keys, 2 ),
				Values = coordinates
			};
		}
	}

	private MorphAnimation ReadMorph( AnimationTrack track, uint offset )
	{
		// The two layouts are selected at 004718E5 / 004718EE.
		return track.HasFloatMorph ? ReadFloatMorph( offset, track.ApparentDuration ) : ReadPackedMorph( offset );
	}

	private MorphAnimation ReadFloatMorph( uint offset, uint duration )
	{
		RequireSection( offset, 12, "float morph header" );
		uint mapOffset = U32( offset ), count = U32( offset + 4 ), values = U32( offset + 8 );
		ulong total = (ulong)count * (duration + 1UL);
		RequireSection( values, total * Vector3Bytes, "float morph frames" );
		var positions = new Vector3[checked((int)total)];
		for ( int i = 0; i < positions.Length; i++ )
		{
			positions[i] = Vector( values + (uint)i * Vector3Bytes );
		}

		var map = Array.Empty<uint>();
		if ( mapOffset != 0 )
		{
			RequireSection( mapOffset, (ulong)count * 4, "morph position map" );
			map = new uint[count];
			for ( uint i = 0; i < count; i++ )
			{
				map[i] = U32( mapOffset + i * 4 );
			}
		}

		return new MorphAnimation
		{
			PositionsPerFrame = checked((int)count),
			FloatPositions = positions,
			PositionMap = map
		};
	}

	private MorphAnimation ReadPackedMorph( uint offset )
	{
		RequireSection( offset, 44, "packed morph header" );
		uint flags = U16( offset ), table = U32( offset + 12 );
		int blockCount = U16( offset + 2 );
		RequireSection( table, (ulong)blockCount * 20, "packed morph blocks" );
		var blocks = new List<MorphBlock>();
		// Block zero contains animated bounds, not vertex indices (0047158F).
		for ( int i = 1; i < blockCount; i++ )
		{
			uint b = table + (uint)i * 20;
			int count = U16( b + 2 );
			bool constant = i == 1 && (flags & 2) != 0;
			int keyCount = constant ? 1 : U16( b );
			uint indicesOffset = U32( b + 4 ), values = U32( b + 12 );
			RequireSection( indicesOffset, (ulong)count * 2, "morph indices" );
			RequireSection( values, (ulong)count * (uint)keyCount * 4, "packed morph values" );
			var indices = new ushort[count];
			for ( int j = 0; j < count; j++ )
			{
				indices[j] = U16( indicesOffset + (uint)j * 2 );
			}

			var packed = new uint[checked(count * keyCount)];
			for ( int j = 0; j < packed.Length; j++ )
			{
				packed[j] = U32( values + (uint)j * 4 );
			}

			blocks.Add( new MorphBlock
			{
				PositionIndices = indices,
				PackedPositions = packed,
				Times = constant ? new ushort[] { 0 } : ReadTimes( U32( b + 8 ), keyCount, 2 )
			} );
		}

		return new MorphAnimation
		{
			Offset = Vector( offset + 20 ),
			Scale = Vector( offset + 32 ),
			Blocks = blocks.ToArray()
		};
	}
}
