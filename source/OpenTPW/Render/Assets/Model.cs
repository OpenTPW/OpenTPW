using OpenTPW.Graphics;

namespace OpenTPW;

/// <summary>Game mesh adapter; geometry, animation uploads and drawing use the shared mesh layer.</summary>
public sealed class Model : Asset
{
	internal MeshGeometry<Vertex> Geometry { get; }
	public Material Material { get; }
	public bool IsIndexed { get; }
	public Model( Vertex[] vertices, uint[] indices, Material material )
	{
		Material = material; IsIndexed = true;
		Geometry = new( Device, vertices, indices ); All.Add( this );
	}
	public Model( Vertex[] vertices, Material material )
	{
		Material = material; Geometry = new( Device, vertices ); All.Add( this );
	}
	public void UpdateVertices( Vertex[] vertices ) { Geometry.UpdateVertices( vertices ); Render.BufferUploads++; }
	internal void Draw() => Geometry.Draw( Render.Frame, Render.Pass, Material.Native );
}
