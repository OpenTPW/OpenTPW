using System.Numerics;
using NumericsVector3 = System.Numerics.Vector3;

namespace OpenTPW;

public sealed partial class ModelAnimationPlayer
{
	// Packed morph positions use three signed ten-bit components in a uint;
	// the remaining two high bits are unused.
	private const int PackedComponentBits = 10;
	private const int IntegerBits = 32;
	private const float MinimumBasisLengthSquared = 1e-20f;
	// Eight stored easing samples plus the implicit endpoints make nine
	// intervals. Match the original's slightly-below-nine multiplier so the
	// final sample cannot index beyond the table.
	private const float EasingSegmentScale = 8.999995f;
	private const int BezierPointsPerSegment = 3;
	private static Matrix4x4 SampleTransform( ModelFile.AnimationTrack track, float time, Matrix4x4 matrix )
	{
		if ( track.HasRotation )
		{
			var (previous, next, fraction) = FindSegment( track.Keys20, time, static k => k.Time );
			if ( previous >= 0 )
			{
				fraction = EaseRotation( track, previous, fraction );
				var previousRotation = track.Keys20[previous].Components;
				var nextRotation = track.Keys20[next].Components;
				var rotation = Quaternion.Slerp( new Quaternion( previousRotation.X, previousRotation.Y, previousRotation.Z, previousRotation.W ),
					new Quaternion( nextRotation.X, nextRotation.Y, nextRotation.Z, nextRotation.W ),
					fraction );
				var translation = matrix.Translation;
				matrix = Matrix4x4.CreateFromQuaternion( Quaternion.Normalize( rotation ) );
				matrix.Translation = translation;
			}
		}

		if ( track.HasScale )
		{
			var (previous, next, fraction) = FindSegment( track.Keys16, time, static k => k.Time );
			if ( previous >= 0 )
			{
				var scale = track.Keys16[previous].Components * (1 - fraction) + track.Keys16[next].Components * fraction;
				// 0046F910 sets basis lengths, preserving its existing orientation.
				var x = SetLength( new( matrix.M11, matrix.M12, matrix.M13 ), scale.X );
				var y = SetLength( new( matrix.M21, matrix.M22, matrix.M23 ), scale.Y );
				var z = SetLength( new( matrix.M31, matrix.M32, matrix.M33 ), scale.Z );
				matrix.M11 = x.X;
				matrix.M12 = x.Y;
				matrix.M13 = x.Z;
				matrix.M21 = y.X;
				matrix.M22 = y.Y;
				matrix.M23 = y.Z;
				matrix.M31 = z.X;
				matrix.M32 = z.Y;
				matrix.M33 = z.Z;
			}
		}

		if ( track.Translation is { } curve && curve.Times.Length > 0 && time >= curve.Times[0] )
		{
			matrix.Translation = SampleTranslation( curve, time ).GetSystemVector3();
		}

		return matrix;
	}

	private static NumericsVector3 SetLength( NumericsVector3 direction, float length )
	{
		// A collapsed basis has no direction to preserve; normalizing it would
		// produce NaNs that spread through the model's world matrix.
		return direction.LengthSquared() > MinimumBasisLengthSquared
			? NumericsVector3.Normalize( direction ) * length
			: NumericsVector3.Zero;
	}

	private static (int A, int B, float Fraction) FindSegment<T>( T[] keys, float time, Func<T, ushort> getTime )
	{
		if ( keys.Length == 0 || time < getTime( keys[0] ) )
		{
			return (-1, -1, 0);
		}

		int low = 0, high = keys.Length;
		while ( low < high )
		{
			int middle = low + (high - low) / 2;
			if ( getTime( keys[middle] ) <= time )
			{
				low = middle + 1;
			}
			else
			{
				high = middle;
			}
		}

		int previous = low - 1, next = Math.Min( low, keys.Length - 1 );
		float fraction = previous == next ? 0 :
			(time - getTime( keys[previous] )) / (getTime( keys[next] ) - getTime( keys[previous] ));
		return (previous, next, fraction);
	}

	private static float EaseRotation( ModelFile.AnimationTrack track, int key, float fraction )
	{
		int easing = track.Keys20[key].Metadata;
		if ( easing == ushort.MaxValue || track.RotationEasing.Length == 0 )
		{
			return fraction;
		}

		float position = fraction * EasingSegmentScale;
		int segment = (int)position;
		int sampleCount = ModelFile.RotationEasingSamples;
		int tableOffset = easing * sampleCount;
		float previous = segment == 0 ? 0 :
			track.RotationEasing[tableOffset + Math.Min( segment - 1, sampleCount - 1 )] / (float)byte.MaxValue;
		float next = segment >= sampleCount ? 1 :
			track.RotationEasing[tableOffset + segment] / (float)byte.MaxValue;
		return previous + (next - previous) * (position - segment);
	}

	public static Vector3 SampleTranslation( ModelFile.TranslationCurve curve, float time )
	{
		var (previous, next, t) = FindSegment( curve.Times, time, static k => k );
		if ( previous < 0 || curve.Points.Length == 0 )
		{
			return Vector3.Zero;
		}

		var points = curve.Points;
		if ( curve.IsCubicBezier )
		{
			int start = Math.Min( previous * BezierPointsPerSegment, points.Length - 1 );
			if ( previous == next )
			{
				return points[start];
			}

			float u = 1 - t;
			// 00474840: cubic Bezier. Adjacent segments share their endpoint;
			// modulo allows the final segment to wrap to the start of the path.
			var control1 = points[(start + 1) % points.Length];
			var control2 = points[(start + 2) % points.Length];
			var endpoint = points[(start + 3) % points.Length];
			return points[start] * (u * u * u) + control1 * (3 * u * u * t)
				+ control2 * (3 * u * t * t) + endpoint * (t * t * t);
		}

		return points[Math.Min( previous, points.Length - 1 )] * (1 - t) + points[Math.Min( next, points.Length - 1 )] * t;
	}

	public static Vector3 DecodePackedPosition( uint packed, Vector3 offset, Vector3 scale ) => new(
		offset.X + DecodePackedComponent( packed, 0 ) * scale.X,
		offset.Y + DecodePackedComponent( packed, PackedComponentBits ) * scale.Y,
		offset.Z + DecodePackedComponent( packed, PackedComponentBits * 2 ) * scale.Z );

	private static int DecodePackedComponent( uint packed, int bitOffset )
	{
		// Move the component's sign bit to bit 31, then sign-extend it.
		int signShift = IntegerBits - PackedComponentBits;
		return (int)(packed << (signShift - bitOffset)) >> signShift;
	}

	private static void SampleMorph( ModelFile.MorphAnimation morph, float time, Vector3[] positions )
	{
		if ( morph.PositionsPerFrame > 0 )
		{
			int last = morph.FloatPositions.Length / morph.PositionsPerFrame - 1;
			int previous = Math.Clamp( (int)time, 0, last ), next = Math.Min( previous + 1, last );
			float fraction = Math.Clamp( time - previous, 0, 1 );
			for ( int i = 0; i < positions.Length; i++ )
			{
				int index = morph.PositionMap.Length == 0 ? i : (int)morph.PositionMap[i];
				positions[i] = morph.FloatPositions[previous * morph.PositionsPerFrame + index] * (1 - fraction) + morph.FloatPositions[next * morph.PositionsPerFrame + index] * fraction;
			}

			return;
		}

		foreach ( var block in morph.Blocks )
		{
			var (previous, next, fraction) = FindSegment( block.Times, time, static k => k );
			if ( previous < 0 )
			{
				continue;
			}

			int count = block.PositionIndices.Length;
			for ( int i = 0; i < count; i++ )
			{
				var previousPosition = DecodePackedPosition( block.PackedPositions[previous * count + i], morph.Offset, morph.Scale );
				var nextPosition = DecodePackedPosition( block.PackedPositions[next * count + i], morph.Offset, morph.Scale );
				positions[block.PositionIndices[i]] = previousPosition * (1 - fraction) + nextPosition * fraction;
			}
		}
	}
}
