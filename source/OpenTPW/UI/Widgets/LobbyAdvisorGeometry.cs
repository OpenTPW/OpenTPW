using System.Numerics;

namespace OpenTPW;

/// <summary>The advisor's independent screen-space model, as configured by TP.exe 0x429650.</summary>
public sealed class LobbyAdvisorGeometry
{
	public const string ModelPath = "global/advisor/advisor.md2";
	public ModelFile Model { get; }
	public Matrix4x4 RootTransform { get; }
	public static readonly System.Numerics.Vector3 ScreenOffset = new( 0.6f, -0.6f, 0.2f );

	/// <summary>
	/// The advisor's own directional light (record +0x48: constructor 0x429510 sets type 2 and colour 1.0;
	/// 0x429650 aims it along (26.67, −26.67, 800) and registers it with mask 0x10, the advisor's own draw list).
	/// </summary>
	public static readonly System.Numerics.Vector3 LightDirection = System.Numerics.Vector3.Normalize( new( 26.666666f, -26.666666f, 800 ) );

	/// <summary>
	/// Vertex colour for a node-to-screen matrix and an authored normal (US 0x5741b0, 0x5743a0). US 0x57aa10 lights
	/// every mesh with the lights of the list being drawn (engine +0x30, here the advisor light) and, unless the
	/// draw flags carry 0x3000000, also the global mask-0x70 list (engine +0x2c). Category lists are drawn with
	/// 0x8bb624 = 0 (0x576910 sets 0x1000000 only for the global list itself), so the advisor also receives the
	/// lobby camera light, which US 0x5d92e0 registers with mask 0x70. That light's world direction (retail axes)
	/// is applied unchanged in the advisor's screen space, so it shifts with the lobby camera.
	/// </summary>
	public static System.Numerics.Vector3 Colour( System.Numerics.Vector3 ambient, Matrix4x4 node, System.Numerics.Vector3 normal,
		System.Numerics.Vector3 sceneDirection, System.Numerics.Vector3 sceneDiffuse )
	{
		if ( !Matrix4x4.Invert( node, out var inverse ) ) throw new InvalidDataException( "Advisor node matrix cannot be inverted for lighting" );
		float Facing( System.Numerics.Vector3 direction )
		{
			var local = System.Numerics.Vector3.TransformNormal( direction, inverse );
			// US 0x5743a0 renormalises unless the squared length is 0 or already 1.
			float length = local.LengthSquared();
			if ( length is not (0 or 1) ) local /= MathF.Sqrt( length );
			return MathF.Max( 0, -System.Numerics.Vector3.Dot( local, normal ) );
		}
		var total = ambient + new System.Numerics.Vector3( Facing( LightDirection ) ) + sceneDiffuse * Facing( sceneDirection );
		return System.Numerics.Vector3.Min( total, System.Numerics.Vector3.One );
	}

	public LobbyAdvisorGeometry()
	{
		byte[] bytes = FileSystem.ReadAllBytes( AnimationFiles.ResolveModel( ModelPath ) );
		Model = new ModelFile( new MemoryStream( bytes ) );
		var root = Model.Nodes.Single( node => node.ParentIndex < 0 );
		// 0x46e210 scales the local basis rows, before the authored root rotation.
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
