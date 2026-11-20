using ImGuiNET;
using Veldrid;

namespace OpenTPW.ModKit;

/// <summary>Previews a game sound through the host audio system.</summary>
[HandlesExtension( ".mp2" )]
internal class SoundViewer : IFileViewer
{
	private readonly string fileName;

	public SoundViewer( string fileName )
	{
		this.fileName = fileName;
	}

	internal void PlaySound()
	{
		var preview = PreviewSound ?? throw new InvalidOperationException( "The host has not connected sound preview" );
		preview( fileName );
	}

	public void DrawPreview()
	{
		if ( ImGui.Button( "Play" ))
		{
			PlaySound();
		}
	}

	public TextureView GetIcon()
	{
		throw new NotImplementedException();
	}
}
