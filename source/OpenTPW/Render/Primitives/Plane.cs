namespace OpenTPW;

partial class Primitives
{
	internal static class Plane
	{
		public static Vertex[] Vertices = new[]
		{
			new Vertex()
			{
				Position = new Vector3(-1.0f, -1.0f, 0.0f),
				TexCoords = new Vector2(0.0f, 0.0f),
				Normal = new Vector3(0.0f, 0.0f, 1.0f),
			},
			new Vertex()
			{
				Position = new Vector3(1.0f, -1.0f, 0.0f),
				TexCoords = new Vector2(1.0f, 0.0f),
				Normal = new Vector3(0.0f, 0.0f, 1.0f),
			},
			new Vertex()
			{
				Position = new Vector3(-1.0f, 1.0f, 0.0f),
				TexCoords = new Vector2(0.0f, 1.0f),
				Normal = new Vector3(0.0f, 0.0f, 1.0f),
			},
			new Vertex()
			{
				Position = new Vector3(1.0f, 1.0f, 0.0f),
				TexCoords = new Vector2(1.0f, 1.0f),
				Normal = new Vector3(0.0f, 0.0f, 1.0f),
			}
		};

		public static uint[] Indices { get; } = new uint[]
		{
			0, 2, 3,
			3, 1, 0,
		};

		public static Mesh Create( Material material, Point2? _repeats = null )
		{
			var repeats = _repeats ?? new Point2( 1, 1 );

			var vertices = new Vertex[4 * repeats.X * repeats.Y];
			var indices = new uint[6 * repeats.X * repeats.Y];

			for ( int y = 0; y < repeats.Y; y++ )
			{
				for ( int x = 0; x < repeats.X; x++ )
				{
					var offset = (y * repeats.X + x) * 4;
					var indexOffset = (y * repeats.X + x) * 6;

					for ( int i = 0; i < 4; i++ )
					{
						var vertex = Vertices[i];
						vertex.Position += new Vector3( x * 2.0f, y * 2.0f, 0 );

						vertices[offset + i] = vertex;
					}

					for ( int i = 0; i < 6; i++ )
					{
						indices[indexOffset + i] = Indices[i] + (uint)offset;
					}
				}
			}

			// Shift to center
			if ( repeats.X > 1 && repeats.Y > 1 )
			{
				var center = new Point2( repeats.X / 2, repeats.Y / 2 );

				for ( int i = 0; i < vertices.Length; i++ )
				{
					vertices[i].Position -= new Vector3( center.X * 2.0f, center.Y * 2.0f, 0 );
				}
			}

			var mesh = new Mesh( vertices, indices, material );
			return mesh;
		}
	}
}
