global using static OpenTPW.Common.GlobalNamespace;
global using static OpenTPW.ModKit.GlobalNamespace;

namespace OpenTPW.ModKit;

using OpenTPW;

public static class GlobalNamespace
{
	/// <summary>Plays a file through the host game's shared audio output; the host owns preview lifetime.</summary>
	public static Action<string>? PreviewSound { get; set; }
}
