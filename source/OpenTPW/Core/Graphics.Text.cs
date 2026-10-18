using Veldrid;

namespace OpenTPW;

partial class Graphics
{
	internal static void TextQuads( IReadOnlyList<TextLayout.Quad> quads, Material material )
	{
		// Preserve painter's order while fitting the shared dynamic buffers.
		const int maxQuads = MaxIndexCount / 6;
		var matrix = CreateScreenMatrix( BaseScreenSize );
		var cmd = Render.GetCommandList();
		material.GetTemporaryResourceSet( out var sets );
		for ( int first = 0; first < quads.Count; first += maxQuads )
		{
			int count = Math.Min( maxQuads, quads.Count - first );
			var vertices = new Vertex[count * 4];
			var indices = new uint[count * 6];
			for ( int i = 0; i < count; ++i )
			{
				var (rect, uv) = quads[first + i];
				vertices[i * 4] = new Vertex( new Vector3( rect.TopLeft ) * matrix, uv.TopLeft );
				vertices[i * 4 + 1] = new Vertex( new Vector3( rect.TopRight ) * matrix, uv.TopRight );
				vertices[i * 4 + 2] = new Vertex( new Vector3( rect.BottomLeft ) * matrix, uv.BottomLeft );
				vertices[i * 4 + 3] = new Vertex( new Vector3( rect.BottomRight ) * matrix, uv.BottomRight );
				uint v = (uint)i * 4;
				indices[i * 6] = v + 3;
				indices[i * 6 + 1] = v + 2;
				indices[i * 6 + 2] = v + 1;
				indices[i * 6 + 3] = v;
				indices[i * 6 + 4] = v + 1;
				indices[i * 6 + 5] = v + 2;
			}

			cmd.UpdateBuffer( vertexBuffer, 0, vertices );
			cmd.UpdateBuffer( indexBuffer, 0, indices );
			cmd.SetIndexBuffer( indexBuffer, IndexFormat.UInt32 );
			cmd.SetVertexBuffer( 0, vertexBuffer );
			cmd.SetPipeline( material.Pipeline );
			for ( uint i = 0; i < sets.Length; ++i )
			{
				cmd.SetGraphicsResourceSet( i, sets[i] );
			}

			cmd.DrawIndexed( (uint)indices.Length );
		}
	}
}
