namespace OpenTPW;

/// <summary>Original heightfield quads, with LND cutouts for modelled scenery.</summary>
public sealed class ParkTerrainActor : StaticModelActor
{
	// The LND control map has four texels per heightfield cell. These offsets
	// align the reconstructed ground with the original modelled scenery.
	private const int ControlTexelsPerCell = 4;
	private const int ControlOffsetX = 1;
	private const int ControlOffsetY = 7;

	public ParkTerrainActor( ParkTerrainFile terrain )
	{
		var vertices = CreateVertices( terrain );
		var indices = CreateIndices( terrain );
		var material = CreateMaterial( terrain );
		Model = new Model( new Mesh( vertices, indices, material ) );
	}

	private static Vertex[] CreateVertices( ParkTerrainFile terrain )
	{
		int rowWidth = terrain.Width + 1;
		var vertices = new Vertex[rowWidth * (terrain.Height + 1)];
		for ( int y = 0; y <= terrain.Height; y++ )
		{
			for ( int x = 0; x <= terrain.Width; x++ )
			{
				vertices[y * rowWidth + x] = new Vertex
				{
					Position = terrain.Position( x, y ),
					Normal = Vector3.Up,
					// LND rows are bottom-up; the world shader flips texture Y.
					TexCoords = new Vector2( (x * ControlTexelsPerCell + ControlOffsetX) / (float)terrain.TextureWidth,
						(y * ControlTexelsPerCell + ControlOffsetY) / (float)terrain.TextureHeight )
				};
			}
		}

		return vertices;
	}

	private static uint[] CreateIndices( ParkTerrainFile terrain )
	{
		var indices = new List<uint>();
		for ( int y = 0; y < terrain.Height; y++ )
		{
			for ( int x = 0; x < terrain.Width; x++ )
			{
				var flags = terrain.GetCellFlags( x, y );
				if ( (flags & ParkTerrainFile.CellFlags.Hidden) != 0 )
				{
					continue;
				}

				uint topLeft = (uint)(y * (terrain.Width + 1) + x);
				uint topRight = topLeft + 1;
				uint bottomLeft = topLeft + (uint)terrain.Width + 1;
				uint bottomRight = bottomLeft + 1;
				if ( (flags & ParkTerrainFile.CellFlags.AlternateDiagonal) != 0 )
				{
					indices.AddRange( [topLeft, bottomLeft, topRight, topRight, bottomLeft, bottomRight] );
				}
				else
				{
					indices.AddRange( [topLeft, bottomLeft, bottomRight, bottomRight, topRight, topLeft] );
				}
			}
		}

		return indices.ToArray();
	}

	private static Material CreateMaterial( ParkTerrainFile terrain )
	{
		var material = new Material<ObjectUniformBuffer>( "content/shaders/terrain.shader" );
		var texture = new Texture( terrain.GroundRgba, terrain.TextureWidth, terrain.TextureHeight );
		// terrain.shader expects control map, six colour textures, seven masks,
		// then the five-row blend lookup. The last slot fills the shared layout.
		var textures = new List<Texture> { texture };
		textures.AddRange( terrain.Materials.Select( image => new Texture( image.Rgba, image.Width, image.Height, TextureFlags.Wrap ) ) );
		textures.Add( new Texture( terrain.BlendLookup, ParkTerrainFile.BlendLookupWidth, ParkTerrainFile.BlendLookupRows, TextureFlags.DistanceField ) );
		textures.Add( texture );
		material.Set( "Color", textures.ToArray() );
		return material;
	}
}
