namespace OpenTPW.ModKit;

/// <summary>A preview panel for one file type, created by the file browser when a file is selected.</summary>
internal interface IFileViewer : IDisposable
{
	void DrawPreview();

	// Most viewers hold nothing beyond managed memory.
	void IDisposable.Dispose() { }
}
