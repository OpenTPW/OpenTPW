global using static Global;
using OpenTPW;
using OpenTPW.Graphics;

/// <summary>Active game renderer and its shared SDL GPU device.</summary>
public static class Global
{
	internal static GpuDevice Device { get; set; } = null!;
	internal static Renderer Render { get; set; } = null!;
}
