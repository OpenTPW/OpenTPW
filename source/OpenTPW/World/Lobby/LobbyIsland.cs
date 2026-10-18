namespace OpenTPW;

public sealed class LobbyIsland : StaticModelActor
{
	private readonly int islandIndex;
	private readonly Model gate;
	public Vector3 GatePosition { get; }
	public Vector3 GateOutward { get; }
	public int IslandIndex => islandIndex;
	public void OpenGate() => gate.PlayAnimation( "m1", loop: false );
	public void ResetGate() => gate.Animation?.Stop();

	protected override void OnRender()
	{
		if ( Level.SelectedPlayer != null && !Level.IsOverview && Level.SelectedIsland != islandIndex )
		{
			return;
		}

		base.OnRender();
		gate.Draw( new Transform { Position = Position, Rotation = new Rotation( Rotation.X, Rotation.Y, Rotation.Z, Rotation.W ) } );
	}

	public LobbyIsland( Vector3 _position, string themeName )
	{
		// Preserve the existing Space fallback for callers using an unknown name.
		islandIndex = Enumerable.Range( 0, ParkThemes.All.Count )
			.FirstOrDefault( index => ParkThemes.Get( index ).LobbyName == themeName, ParkThemes.All.Count - 1 );
		var theme = ParkThemes.Get( islandIndex );
		Position = _position;
		// ISLAND records in the original lobby configuration include a heading.
		float heading = theme.LobbyHeading;
		Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, heading * MathF.PI / 180 );
		var modelPrefix = theme.LobbyModelPrefix;
		var islePath = $"lobby/terrain/{modelPrefix}_isle.md2";
		Model = new Model( islePath, Material.World );
		// The lobby's ambient motion is stored in the island's M1 companion.
		Model.PlayAnimation( "m1" );
		string gatePath = $"lobby/terrain/{modelPrefix}_gate.md2";
		gate = new Model( gatePath, Material.World );
		var file = new ModelFile( gatePath );
		var points = file.MeshDataList.SelectMany( m => m.Vertices.Select( v =>
			System.Numerics.Vector3.Transform( v.Position.GetSystemVector3(), m.TransformMatrix ) ) ).ToArray();
		var min = points.Aggregate( new System.Numerics.Vector3( float.MaxValue ), System.Numerics.Vector3.Min );
		var max = points.Aggregate( new System.Numerics.Vector3( float.MinValue ), System.Numerics.Vector3.Max );
		var center = (min + max) / 2;
		var world = System.Numerics.Vector3.Transform( new System.Numerics.Vector3( center.X, center.Z, center.Y ), Rotation );
		GatePosition = Position + new Vector3( world.X, world.Y, world.Z );
		GateOutward = (GatePosition - Position).WithZ( 0 ).Normal;
	}
}
