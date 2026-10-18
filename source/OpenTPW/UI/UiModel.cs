namespace OpenTPW.UI;

internal static class UiModel
{
	public static Model Load( string path, Vector2 size, Func<Vector3, Vector2> project, bool firstMeshOnly = false, bool flipVertical = true )
	{
		var source = new ModelFile( path );
		var sourceMeshes = (firstMeshOnly ? source.MeshDataList.Take( 1 ) : source.MeshDataList).ToArray();
		var points = sourceMeshes.SelectMany( mesh => mesh.Vertices ).Select( vertex => project( vertex.Position ) ).ToArray();
		float minX = points.Min( point => point.X );
		float maxX = points.Max( point => point.X );
		float minY = points.Min( point => point.Y );
		float maxY = points.Max( point => point.Y );
		var meshes = new List<Mesh>();
		var materials = new ModelMaterialBuilder( path, Material.UIMesh );
		foreach ( var mesh in sourceMeshes )
		{
			foreach ( var batch in ModelGeometry.CreateBatches( mesh ) )
			{
				var material = materials.Create( batch.Materials );
				for ( int i = 0; i < batch.Vertices.Length; i++ )
				{
					var point = project( mesh.Vertices[batch.SourceCorners[i]].Position );
					float y = flipVertical ? maxY - point.Y : point.Y - minY;
					batch.Vertices[i].Position = new Vector3( (point.X - minX) / (maxX - minX) * size.X, 0, y / (maxY - minY) * size.Y );
				}
				// Flipping the screen axis also reverses triangle winding.
				if ( !flipVertical )
				{
					for ( int i = 0; i < batch.Indices.Length; i += 3 )
					{
						(batch.Indices[i + 1], batch.Indices[i + 2]) = (batch.Indices[i + 2], batch.Indices[i + 1]);
					}
				}

				meshes.Add( new Mesh( batch.Vertices, batch.Indices, material ) );
			}
		}

		return new Model( meshes.ToArray() );
	}
}
