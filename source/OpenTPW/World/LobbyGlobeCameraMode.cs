namespace OpenTPW;

/// <summary>Globe page camera. 0x5d8060 sets a 60 degree retail FOV, converted for the retail +/-0.5 viewport like the island camera.</summary>
public sealed class LobbyGlobeCameraMode : CameraMode
{
	internal static LobbyGlobeState State { get; set; } = null!;
	public override void Update()
	{
		State.Update( RetailTime.FrameTicks, new System.Numerics.Vector2( Input.Mouse.Position.X / Screen.Size.X, Input.Mouse.Position.Y / Screen.Size.Y ) );
		Position = State.Position;
		Rotation = Rotation.LookAt( -Position );
		// The globe camera looks at the origin; the shared camera light follows it (0x5d3eb0).
		Level.Current?.Lighting.Track( Position.GetSystemVector3(), System.Numerics.Vector3.Zero );
		FieldOfView = (2 * MathF.Atan( 0.5f * MathF.Tan( 60f.DegreesToRadians() * 0.5f ) )).RadiansToDegrees();
	}
}
