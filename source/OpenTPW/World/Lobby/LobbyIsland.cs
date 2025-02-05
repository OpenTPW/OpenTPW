namespace OpenTPW;

public sealed class LobbyIsland : ModelEntity
{
	public LobbyIsland( Vector3 _position, string themeName )
	{
		Position = _position;

		var modelPrefix = themeName[0..3];
		var islePath = $"lobby/terrain/{modelPrefix}_isle.md2";

		Model = new Model( islePath, Material.Test );
	}
}
