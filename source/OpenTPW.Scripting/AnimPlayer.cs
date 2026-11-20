namespace OpenTPW;

/// <summary>
/// Plays one <see cref="ModelAnimation"/> clip at a time and defers a new request while a clip is still running
/// (ModelAnimation_QueueSegment, US 0x4732a0 / TPI 0x4df610), so scripted sequences join without a jump.
/// </summary>
public sealed class AnimPlayer
{
	private readonly record struct Clip( ModelAnimation? Anim, bool Loop, float Speed, int StartMs );

	private Clip current;
	private Clip? queued;

	private static float FramesPerMs( Clip clip ) => clip.Speed * (ModelAnimation.FramesPerSecond / 1000f);

	/// <summary>
	/// Starts or queues <paramref name="clip"/> at <paramref name="nowMs"/>. Returns the clip duration in milliseconds at
	/// speed 1 (Animation_ComputeCurrentFrame, US 0x472c60 / TPI 0x4ded80), or 1000 when the clip is missing
	/// (ModelAnimation_QueueSegment, US 0x4732a0 / TPI 0x4df610).
	/// </summary>
	public int Play( ModelAnimation? clip, bool loop, float speed, int nowMs )
	{
		Sample( nowMs );
		var next = new Clip( clip, loop, speed, nowMs );
		bool busy = false;
		if ( current.Anim != null )
		{
			float frames = (nowMs - current.StartMs) * FramesPerMs( current );
			busy = current.Loop || frames < current.Anim.Length;
		}
		if ( busy )
			queued = next;
		else
		{
			current = next;
			queued = null;
		}
		// Missing animations return 1000 ms (0x3e8) in retail US 0x4733e7 / TPI 0x4df610 so RideVM WAITANIM/TRIGANIM do not wrap around 0.
		return clip != null ? (int)(clip.Length * (1000f / ModelAnimation.FramesPerSecond)) : 1000;
	}

	/// <summary>Clears any queued clip (ModelAnimation_FlushQueue, US 0x473270 / TPI 0x4df5d0, invoked by Script_Step FLUSHANIM).</summary>
	public void Flush() => queued = null;

	/// <summary>
	/// Returns the active clip and absolute MD2 frame at <paramref name="nowMs"/>; a finished one-shot clip holds its last frame.
	/// </summary>
	public (ModelAnimation? Clip, float Frame) Sample( int nowMs )
	{
		while ( current.Anim != null )
		{
			float len = current.Anim.Length;
			if ( len <= 0 ) return (current.Anim, current.Anim.StartFrame);
			float fpm = FramesPerMs( current );
			float elapsed = (nowMs - current.StartMs) * fpm;
			if ( elapsed < len )
				return (current.Anim, current.Anim.StartFrame + Math.Max( elapsed, 0f ));
			if ( queued is { } next )
			{
				float cycles = fpm > 0 ? MathF.Floor( elapsed / len ) : 0f;
				int startMs = fpm > 0 ? current.StartMs + (int)(cycles * len / fpm) : nowMs;
				current = next with { StartMs = startMs };
				queued = null;
				continue;
			}
			if ( current.Loop )
				return (current.Anim, current.Anim.StartFrame + (elapsed % len));
			return (current.Anim, current.Anim.StartFrame + len);
		}
		return (null, 0f);
	}
}
