namespace OpenTPW;

public sealed class ParkEntryCameraMode : CameraMode
{
	public const float Duration = 2.5f;
	// Reconstructed camera framing in world units. The first 65% approaches
	// the gate; the rest carries the camera through its doors.
	private const float ApproachFraction = 0.65f;
	private const float ApproachDistance = 28;
	private const float ApproachHeight = 3;
	private const float EntryDistance = 7;
	private const float FocusDistance = 20;
	private const float FinalFieldOfView = 60;
	internal static Vector3 StartPosition { get; set; }
	internal static Vector3 StartFocus { get; set; }
	internal static Vector3 Gate { get; set; }
	internal static Vector3 Outward { get; set; }
	internal static float StartFov { get; set; }
	private readonly float started = Time.Now;
	public override void Update()
	{
		float progress = ((Time.Now - started) / Duration).Clamp( 0, 1 );
		float easedProgress = SmoothStep( progress );
		var approach = Gate + Outward * ApproachDistance + Vector3.Up * ApproachHeight;
		// Settle in front of the gate before passing between its animated doors.
		if ( progress < ApproachFraction )
		{
			float approachProgress = (progress / ApproachFraction).Clamp( 0, 1 );
			Position = StartPosition.LerpTo( approach, SmoothStep( approachProgress ) );
		}
		else
		{
			float entryProgress = (progress - ApproachFraction) / (1 - ApproachFraction);
			Position = approach.LerpTo( Gate - Outward * EntryDistance, entryProgress );
		}

		var focus = StartFocus.LerpTo( Gate - Outward * FocusDistance, easedProgress );
		Rotation = Rotation.LookAt( focus - Position );
		FieldOfView = StartFov + (FinalFieldOfView - StartFov) * easedProgress;
	}

	// Cubic smoothstep gives zero velocity at both ends of an interpolation.
	private static float SmoothStep( float progress ) => progress * progress * (3 - 2 * progress);
}
