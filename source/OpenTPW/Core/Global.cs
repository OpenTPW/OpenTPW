global using static Global;
using OpenTPW;
using Veldrid;

public static class Global
{
	/// <summary>
	/// The device currently being used for rendering.
	/// </summary>
	internal static GraphicsDevice Device { get; set; } = null!;

	/// <summary>
	/// The current active renderer.
	/// </summary>
	internal static Renderer Render { get; set; } = null!;
}
