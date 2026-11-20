using System.Numerics;

namespace OpenTPW;

/// <summary>The advisor's independent screen-space model, as configured by TP.exe Advisor_ConfigureScreenModel (US 0x429ba0).</summary>
public sealed class LobbyAdvisorGeometry
{
	public const string ModelPath = "global/advisor/advisor.md2";
	public ModelFile Model { get; }
	public Matrix4x4 RootTransform { get; }
	public static readonly System.Numerics.Vector3 ScreenOffset = new( 0.6f, -0.6f, 0.2f );

	/// <summary>
	/// The advisor's own directional light (record +0x48: constructor AdvisorLight_Construct (US 0x429a60) sets type 2 and colour 1.0;
	/// Advisor_ConfigureScreenModel (US 0x429ba0) aims it along (26.67, −26.67, 800) and registers it with mask 0x10, the advisor's own draw list).
	/// </summary>
	public static readonly System.Numerics.Vector3 LightDirection = System.Numerics.Vector3.Normalize( new( 26.666666f, -26.666666f, 800 ) );

	/// <summary>
	/// The advisor light's direction in a node's own space, as Render_NormalizeVector (US 0x5743a0) leaves it at light +0x38:
	/// the node-chain matrix that Model_SubmitNodeTree (US 0x57a530) builds for lighting (local matrices from the mesh node up
	/// to the root, including the root basis scaled by Model_ScaleLocalBasis (US 0x46fa70)) is inverted by Matrix_Invert (US 0x578b20),
	/// the direction is multiplied as a row vector by its upper 3x3 and then renormalised. Because the root is flattened to 0.001
	/// along the screen depth, the inverse stretches that axis a thousandfold: after renormalising, the light runs along the
	/// model's depth axis whatever its authored tilt, so the model is shaded as if lit head-on before flattening.
	/// The chain starts from the renderable's lighting matrix (+0x50), which its constructor FUN_00579cd0 (US 0x579cd0) sets to
	/// identity; only Render_ApplyModelScreenTransform (US 0x57a260) with mode bit 2 would overwrite it, and none of its three callers passes that
	/// bit (they set only the screen matrix +0x10). So <paramref name="node"/> is the node chain alone.
	/// </summary>
	public static System.Numerics.Vector3 LocalLight( Matrix4x4 node )
	{
		if ( !Matrix4x4.Invert( node, out var inverse ) ) throw new InvalidDataException( "Advisor node matrix cannot be inverted for lighting" );
		var local = System.Numerics.Vector3.TransformNormal( LightDirection, inverse );
		// Render_NormalizeVector (US 0x5743a0) renormalises unless the squared length is 0 or already 1.
		float length = local.LengthSquared();
		if ( length is not (0 or 1) ) local /= MathF.Sqrt( length );
		return local;
	}

	/// <summary>
	/// Vertex colour from <see cref="LocalLight"/> and an authored normal: ambient plus max(0, −L·N) of the white advisor light,
	/// clamped (FUN_005741b0 (US 0x5741b0), Lighting_ComputeVertexColors (US 0x574660)).
	/// The advisor light is the only one. Render_ApplyLightLists (US 0x57aa10) collects the lights of the list being drawn
	/// (engine +0x30, the advisor's mask-0x10 list holding this light) and adds the global mask-0x70 list (engine +0x2c, where
	/// Lobby_SetCameraLight (US 0x5d92e0) registers the lobby camera light) only when the draw flags lack 0x3000000.
	/// Advisor_ConfigureScreenModel (US 0x429ba0) marks the model with Model_MarkScreenSpace (US 0x45bed0) (model flag 0x200), and
	/// ModelInstance_Submit (US 0x57a750) sets gRenderListFlags (US 0x8bb624) bit 0x2000000 for such models, so the scene lights never
	/// reach the advisor. Normals are the model's static ones even on vertex-animated hands: the instance is created with
	/// flags 0x2117, without the bit (8) that gives FUN_00454a10 (US 0x454a10) a private normal array.
	/// </summary>
	public static System.Numerics.Vector3 Colour( System.Numerics.Vector3 ambient, System.Numerics.Vector3 localLight, System.Numerics.Vector3 normal )
	{
		var total = ambient + new System.Numerics.Vector3( MathF.Max( 0, -System.Numerics.Vector3.Dot( localLight, normal ) ) );
		return System.Numerics.Vector3.Min( total, System.Numerics.Vector3.One );
	}

	/// <summary>
	/// Whether retail draws a triangle: Render_CullBackfaces (US 0x581320), called per face by FUN_00580530 (US 0x580530), keeps it
	/// when the determinant of its three (x, y, rhw) screen vertices is positive, i.e. when it winds clockwise on a
	/// screen whose y grows downwards. The test runs on projected vertices, so it needs no correction for mirrored
	/// node matrices. Points here are <see cref="Project"/> output (y up), hence the negated area.
	/// </summary>
	public static bool FacesViewer( Vector3 a, Vector3 b, Vector3 c )
		=> (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y) < 0;

	public LobbyAdvisorGeometry()
	{
		byte[] bytes = FileSystem.ReadAllBytes( AnimationFiles.ResolveModel( ModelPath ) );
		Model = new ModelFile( new MemoryStream( bytes ) );
		var root = Model.Nodes.Single( node => node.ParentIndex < 0 );
		// Model_ScaleLocalBasis (US 0x46fa70) scales the local basis rows, before the authored root rotation.
		root.LocalTransform = Matrix4x4.CreateScale( 0.01125f, 0.001f, 0.015f ) * root.LocalTransform;
		RootTransform = root.LocalTransform;
	}

	public ModelPose Pose( ModelAnimation animation, float frame )
	{
		return animation.Evaluate( Model, frame, ModelAnimation.SegmentHiding.NodeId );
	}

	public static Vector3 Project( System.Numerics.Vector3 point )
	{
		point += ScreenOffset;
		return new Vector3( point.X, point.Y, point.Z );
	}
}
