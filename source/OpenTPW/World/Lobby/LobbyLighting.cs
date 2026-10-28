using N = System.Numerics;

namespace OpenTPW;

/// <summary>
/// Retail vertex lighting. TP.exe 0x570ca0 colours each vertex with 0x5707f0: the ambient colour plus,
/// for every directional light (type 2), its colour × max(0, −L·N), clamped to 1 and modulated with the
/// texture (0x563c50 colour op 4). L is the light's world direction moved into the node's space with
/// the inverse node matrix and renormalised (0x5709e0); N is the authored normal (mesh +0x64).
/// <para>
/// The lobby light belongs to the camera: every camera update (0x5d3eb0) aims it from a point offset
/// from the eye (right × <see cref="RightOffset"/>, up × <see cref="UpOffset"/>) at the camera target;
/// with <see cref="Flatten"/> the vertical part is dropped and −0.3 added before renormalising. Island
/// pages use ambient 0x444444, light 0xeeeeee, offsets 8/2 and flattening (0x5dc390 → 0x5d4300 /
/// 0x5d43b0, camera +0x28/+0x2c/+0x30); the globe page uses 0x202020, 0xffffff and offsets −300/300
/// without flattening (0x5d8060, page +0x38 = 300 from 0x5d7696). Intensity is 1.0 (0x6fe940).
/// </para>
/// </summary>
internal sealed class LobbyLighting
{
	internal static readonly LobbyLighting Islands = new( 0x444444, 0xeeeeee, 2, 8, true );
	internal static readonly LobbyLighting Globe = new( 0x202020, 0xffffff, 300, -300, false );

	internal N.Vector3 Ambient { get; }
	internal N.Vector3 Diffuse { get; }
	internal float UpOffset { get; }
	internal float RightOffset { get; }
	internal bool Flatten { get; }

	/// <summary>The light's travel direction in OpenTPW world axes, updated with the camera.</summary>
	internal static N.Vector3 WorldDirection { get; private set; } = N.Vector3.UnitX;
	/// <summary>The same direction in retail axes (Y up), as stored in the light's +0x2c.</summary>
	internal static N.Vector3 RetailDirection => ToRetail( WorldDirection );

	internal LobbyLighting( uint ambient, uint diffuse, float upOffset, float rightOffset, bool flatten )
	{
		Ambient = Rgb( ambient );
		Diffuse = Rgb( diffuse );
		UpOffset = upOffset;
		RightOffset = rightOffset;
		Flatten = flatten;
	}

	internal static N.Vector3 Rgb( uint colour ) => new( (colour >> 16 & 255) / 255f, (colour >> 8 & 255) / 255f, (colour & 255) / 255f );

	/// <summary>Re-aims the light from a camera pose given in OpenTPW axes (0x5d3eb0).</summary>
	internal void Track( N.Vector3 eye, N.Vector3 target ) => WorldDirection = FromRetail( Aim( ToRetail( eye ), ToRetail( target ) ) );

	/// <summary>0x5d3eb0 in retail axes (Y up).</summary>
	internal N.Vector3 Aim( N.Vector3 eye, N.Vector3 target )
	{
		var forward = N.Vector3.Normalize( target - eye );
		var right = new N.Vector3( forward.Z, 0, -forward.X );
		right = right == N.Vector3.Zero ? N.Vector3.UnitX : N.Vector3.Normalize( right );
		var up = N.Vector3.Normalize( N.Vector3.Cross( forward, right ) );
		var direction = target - (eye + right * RightOffset + up * UpOffset);
		if ( Flatten ) direction.Y = 0;
		direction = N.Vector3.Normalize( direction );
		if ( Flatten ) direction = N.Vector3.Normalize( direction - new N.Vector3( 0, 0.3f, 0 ) );
		return direction;
	}

	/// <summary>The vertex colour 0x5707f0 produces for a node-space light direction and normal.</summary>
	internal N.Vector3 Colour( N.Vector3 localDirection, N.Vector3 normal )
	{
		// Retail adds a light only when −L·N is not negative (sign test at 0x570865); zero normals get ambient only.
		float facing = MathF.Max( 0, -N.Vector3.Dot( localDirection, normal ) );
		return N.Vector3.Min( Ambient + Diffuse * facing, N.Vector3.One );
	}

	/// <summary>
	/// <paramref name="world"/> (OpenTPW axes) in a node's space. <paramref name="node"/> is the node-to-model
	/// matrix in retail axes; <paramref name="root"/> is the model's rotation in OpenTPW axes.
	/// </summary>
	internal static N.Vector3 LocalDirection( N.Vector3 world, N.Matrix4x4 node, N.Quaternion root )
	{
		var retail = ToRetail( N.Vector3.Transform( world, N.Quaternion.Inverse( root ) ) );
		if ( !N.Matrix4x4.Invert( node, out var inverse ) ) throw new InvalidDataException( "Lobby node matrix cannot be inverted for lighting" );
		var local = N.Vector3.TransformNormal( retail, inverse );
		// 0x5709e0 renormalises unless the squared length is 0 or already 1.
		float length = local.LengthSquared();
		return length is 0 or 1 ? local : local / MathF.Sqrt( length );
	}

	private static N.Vector3 ToRetail( N.Vector3 v ) => new( v.X, v.Z, v.Y );
	private static N.Vector3 FromRetail( N.Vector3 v ) => new( v.X, v.Z, v.Y );
}
