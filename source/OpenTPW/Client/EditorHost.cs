using System.Numerics;
using ImGuiNET;
using OpenTPW.Graphics;
using OpenTPW.ModKit;

namespace OpenTPW;

/// <summary>
/// Connects the ModKit editor to the game loop: creates it on first use, feeds it the frame's input and
/// draws it over the game after the scene. It is created lazily because most sessions never open it.
/// </summary>
internal sealed class EditorHost : IDisposable
{
	private readonly GpuDevice device;
	private ImGuiGpuRenderer? renderer;
	private Editor? editor;
	private readonly EditorInput input = new();
	// ImGui's own modifier keys are sent whenever the host's modifier state changes.
	private ModifierKeys lastModifiers;

	public EditorHost( GpuDevice device ) => this.device = device;

	public bool Visible => editor?.shouldRender == true;

	public void Toggle( Func<string, long>? previewSound )
	{
		if ( editor == null )
		{
			renderer = new ImGuiGpuRenderer( device, "content/shaders/compiled" );
			editor = new Editor( renderer );
			ModKit.GlobalNamespace.PreviewSound = previewSound == null ? null : path => previewSound( path );
		}
		editor.shouldRender = !editor.shouldRender;
	}

	/// <summary>Builds the editor's panels for this frame. Texture uploads happen here, before the frame is recorded.</summary>
	public void Update( InputSnapshot snapshot, Vector2 displaySize, float deltaSeconds )
	{
		input.MousePosition = snapshot.MousePosition;
		input.MouseDown[0] = snapshot.IsMouseDown( MouseButton.Left );
		input.MouseDown[1] = snapshot.IsMouseDown( MouseButton.Right );
		input.MouseDown[2] = snapshot.IsMouseDown( MouseButton.Middle );
		input.WheelDelta = snapshot.WheelDelta;
		input.Text.Clear();
		input.Text.AddRange( snapshot.KeyCharPresses );
		input.Keys.Clear();
		foreach ( var ev in snapshot.KeyEvents )
		{
			if ( ev.Modifiers != lastModifiers )
			{
				lastModifiers = ev.Modifiers;
				input.Keys.Add( (ImGuiKey.ModCtrl, ev.Modifiers.HasFlag( ModifierKeys.Control )) );
				input.Keys.Add( (ImGuiKey.ModShift, ev.Modifiers.HasFlag( ModifierKeys.Shift )) );
				input.Keys.Add( (ImGuiKey.ModAlt, ev.Modifiers.HasFlag( ModifierKeys.Alt )) );
				input.Keys.Add( (ImGuiKey.ModSuper, ev.Modifiers.HasFlag( ModifierKeys.Gui )) );
			}
			if ( MapKey( ev.Key ) is { } key ) input.Keys.Add( (key, ev.Down) );
		}
		editor!.UpdateFrom( input, displaySize, deltaSeconds );
	}

	public void Render( GpuFrame frame, GpuRenderPass pass ) => editor!.Render( frame, pass );

	/// <summary>The keys ImGui panels use for navigation, editing and shortcuts; other keys stay with the game.</summary>
	internal static ImGuiKey? MapKey( Key key ) => key switch
	{
		>= Key.A and <= Key.Z => ImGuiKey.A + (key - Key.A),
		>= Key.Number1 and <= Key.Number9 => ImGuiKey._1 + (key - Key.Number1),
		Key.Number0 => ImGuiKey._0,
		>= Key.F1 and <= Key.F12 => ImGuiKey.F1 + (key - Key.F1),
		Key.Tab => ImGuiKey.Tab,
		Key.Left => ImGuiKey.LeftArrow,
		Key.Right => ImGuiKey.RightArrow,
		Key.Up => ImGuiKey.UpArrow,
		Key.Down => ImGuiKey.DownArrow,
		Key.PageUp => ImGuiKey.PageUp,
		Key.PageDown => ImGuiKey.PageDown,
		Key.Home => ImGuiKey.Home,
		Key.End => ImGuiKey.End,
		Key.Insert => ImGuiKey.Insert,
		Key.Delete => ImGuiKey.Delete,
		Key.BackSpace => ImGuiKey.Backspace,
		Key.Space => ImGuiKey.Space,
		Key.Enter => ImGuiKey.Enter,
		Key.KeypadEnter => ImGuiKey.KeypadEnter,
		Key.Escape => ImGuiKey.Escape,
		_ => null
	};

	public void Dispose()
	{
		ModKit.GlobalNamespace.PreviewSound = null;
		renderer?.Dispose();
		renderer = null;
		editor = null;
	}
}
