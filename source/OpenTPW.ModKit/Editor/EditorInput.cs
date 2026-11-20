using System.Numerics;
using ImGuiNET;

namespace OpenTPW.ModKit;

/// <summary>
/// One frame of host input for the editor, already translated to ImGui terms so the editor library does
/// not depend on the game's key or window types.
/// </summary>
public sealed class EditorInput
{
	/// <summary>Pointer position in target pixels.</summary>
	public Vector2 MousePosition { get; set; }
	/// <summary>Held state of the left, right and middle buttons (ImGui button indices 0, 1, 2).</summary>
	public bool[] MouseDown { get; } = new bool[3];
	public float WheelDelta { get; set; }
	/// <summary>Typed characters of the frame, in order.</summary>
	public List<char> Text { get; } = new();
	/// <summary>Key transitions of the frame, in order. Modifier keys are sent as <see cref="ImGuiKey.ModCtrl"/> and friends.</summary>
	public List<(ImGuiKey Key, bool Down)> Keys { get; } = new();
}
