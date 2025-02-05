namespace OpenTPW;

static public class Easings
{
	private const float PI = MathF.PI;
	private const float HALFPI = MathF.PI / 2.0f;

	/// <summary>
	/// Linear interpolation - useful as a baseline and for simple transitions
	/// </summary>
	static public float Linear( float p )
	{
		return p;
	}

	/// <summary>
	/// Smooth acceleration from zero velocity
	/// </summary>
	static public float EaseIn( float p )
	{
		return p * p * p;
	}

	/// <summary>
	/// Smooth deceleration to zero velocity
	/// </summary>
	static public float EaseOut( float p )
	{
		float f = (p - 1);
		return f * f * f + 1;
	}

	/// <summary>
	/// Smooth acceleration and deceleration
	/// </summary>
	static public float EaseInOut( float p )
	{
		if ( p < 0.5f )
		{
			return 4 * p * p * p;
		}
		else
		{
			float f = ((2 * p) - 2);
			return 0.5f * f * f * f + 1;
		}
	}

	/// <summary>
	/// Natural-looking elastic motion with overshoot
	/// </summary>
	static public float Elastic( float p )
	{
		return MathF.Sin( -13 * HALFPI * (p + 1) ) * MathF.Pow( 2, -10 * p ) + 1;
	}

	/// <summary>
	/// Bouncing effect, useful for playful animations
	/// </summary>
	static public float Bounce( float p )
	{
		if ( p < 4 / 11.0f )
		{
			return (121 * p * p) / 16.0f;
		}
		else if ( p < 8 / 11.0f )
		{
			return (363 / 40.0f * p * p) - (99 / 10.0f * p) + 17 / 5.0f;
		}
		else if ( p < 9 / 10.0f )
		{
			return (4356 / 361.0f * p * p) - (35442 / 1805.0f * p) + 16061 / 1805.0f;
		}
		else
		{
			return (54 / 5.0f * p * p) - (513 / 25.0f * p) + 268 / 25.0f;
		}
	}
}
