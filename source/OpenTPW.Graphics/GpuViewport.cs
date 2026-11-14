namespace OpenTPW.Graphics;

/// <summary>Raster viewport in render-target pixels, snapshotted independently for each draw.</summary>
public readonly record struct GpuViewport( float X, float Y, float Width, float Height );
