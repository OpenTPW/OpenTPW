namespace OpenTPW;

/// <summary>Partitions complete triangles to fit the world shader's sixteen texture bindings.</summary>
internal static class LobbyModelBatches
{
	internal sealed record Batch( int FirstMaterial, bool Wrap, uint[] Indices );
	public static IReadOnlyList<Batch> Create( ModelFile.Mesh mesh )
	{
		var batches = new SortedDictionary<(int FirstMaterial, bool Wrap), List<uint>>();
		if ( mesh.Indices.Length % 3 != 0 ) throw new InvalidDataException( "Incomplete model triangle" );
		for ( int face = 0; face < mesh.Indices.Length; face += 3 )
		{
			uint a = mesh.Indices[face], b = mesh.Indices[face + 1], c = mesh.Indices[face + 2];
			uint material = mesh.Vertices[a].TextureIndex;
			if ( material >= mesh.Materials.Length || mesh.Vertices[b].TextureIndex != material || mesh.Vertices[c].TextureIndex != material )
				throw new InvalidDataException( "Model triangle has inconsistent material indices" );
			int start = (int)material / 16 * 16;
			// FUN_00575e10 (US 0x575e10) -> Render_ConvertMaterialFlags (US 0x581790) -> Render_SetD3dState (US 0x567620)/Render_SelectTextureAddressMode (US 0x567a70): repeat flags select WRAP, otherwise CLAMP.
			var key = (start, (mesh.Materials[material].Flags & 0x30) != 0);
			if ( !batches.TryGetValue( key, out var indices ) ) batches.Add( key, indices = new() );
			indices.Add( a ); indices.Add( b ); indices.Add( c );
		}
		return batches.Select( pair => new Batch( pair.Key.FirstMaterial, pair.Key.Wrap, pair.Value.ToArray() ) ).ToArray();
	}
}
