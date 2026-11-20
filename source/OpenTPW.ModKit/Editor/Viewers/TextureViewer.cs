using ImGuiNET;
using System.Numerics;

namespace OpenTPW.ModKit;

[HandlesExtension( ".wct" )]
internal class TextureViewer : IFileViewer
{
	private readonly TextureFile textureFile;
	private IntPtr imguiPtr;

	public TextureViewer( string fileName )
	{
		textureFile = new TextureFile( fileName );
		CreateGpuTexture();
	}

	private void CreateGpuTexture()
	{
		var t = textureFile.Data;
		if ( t.Width == 0 || t.Height == 0 )
			return;

		imguiPtr = Editor.Instance.Renderer.CreateTexture( t.Data, (uint)t.Width, (uint)t.Height );
	}

	public void DrawPreview()
	{
		if ( imguiPtr == IntPtr.Zero )
		{
			ImGui.Text( $"No imguiPtr?" );
			return;
		}

		ImGui.Image( imguiPtr, new Vector2( textureFile.Data.Width, textureFile.Data.Height ) );
	}

	public void Dispose()
	{
		if ( imguiPtr == IntPtr.Zero ) return;
		Editor.Instance.Renderer.DestroyTexture( imguiPtr );
		imguiPtr = IntPtr.Zero;
	}
}
