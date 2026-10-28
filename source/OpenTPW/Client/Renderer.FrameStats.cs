using System.Diagnostics;

namespace OpenTPW;

/// <summary>
/// Debug frame statistics, logged every ten seconds: frame interval, CPU time spent in update and
/// render, and how many command lists and buffer uploads each frame issued.
/// </summary>
partial class Renderer
{
	private readonly Stopwatch statsClock = Stopwatch.StartNew();
	private double statsStart, statsCpu, statsWorstFrame, lastFrameEnd, statsUpdate, statsRender, statsPresent;
	private int statsFrames, gcBefore;
	private double frameUpdate, frameRender, frameWait;
	internal int Submissions, BufferUploads;
	private long statsSubmissions, statsUploads;

	private double FrameBegin() => statsClock.Elapsed.TotalMilliseconds;
	private double Mark() => statsClock.Elapsed.TotalMilliseconds;
	private void AddUpdate( double ms ) { statsUpdate += ms; frameUpdate = ms; }
	private void AddRender( double ms ) { statsRender += ms; frameRender = ms; }
	private void AddWait( double ms ) => frameWait = ms;
	private void AddPresent( double ms ) => statsPresent += ms;

	private void FrameEnd( double begin )
	{
		double now = statsClock.Elapsed.TotalMilliseconds;
		if ( lastFrameEnd > 0 ) statsWorstFrame = Math.Max( statsWorstFrame, now - lastFrameEnd );
		int gc = GC.CollectionCount( 0 ) + GC.CollectionCount( 1 ) * 1000 + GC.CollectionCount( 2 ) * 1000000;
#if DEBUG
		if ( lastFrameEnd > 0 && now - lastFrameEnd > 34 )
			Log.Info( $"Slow frame: {now - lastFrameEnd:F1} ms (wait {frameWait:F1}, update {frameUpdate:F1}, render {frameRender:F1}, GC gen0/1/2 +{(gc - gcBefore) % 1000}/{(gc - gcBefore) / 1000 % 1000}/{(gc - gcBefore) / 1000000})" );
#endif
		gcBefore = gc;
		lastFrameEnd = now;
		statsCpu += now - begin;
		statsFrames++;
		statsSubmissions += Submissions; statsUploads += BufferUploads;
		Submissions = BufferUploads = 0;
		if ( now - statsStart < 10000 ) return;
		double seconds = (now - statsStart) / 1000;
#if DEBUG
		Log.Info( $"Frame stats: {statsFrames / seconds:F1} fps, CPU {statsCpu / statsFrames:F2} ms/frame, worst interval {statsWorstFrame:F1} ms, "
			+ $"update {statsUpdate / statsFrames:F2} ms, render {statsRender / statsFrames:F2} ms, submit+present {statsPresent / statsFrames:F2} ms, "
			+ $"{(double)statsSubmissions / statsFrames:F1} submissions and {(double)statsUploads / statsFrames:F1} buffer uploads per frame" );
#endif
		statsStart = now; statsCpu = statsWorstFrame = statsUpdate = statsRender = statsPresent = 0; statsFrames = 0; statsSubmissions = statsUploads = 0;
	}
}
