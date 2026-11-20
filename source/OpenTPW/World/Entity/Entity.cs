using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using Veldrid;

namespace OpenTPW;

/// <summary>
/// Render pass an entity belongs to. Passes are drawn in ascending order, so the
/// interface always lands on top of the 3D scene no matter when either was created.
/// </summary>
public enum RenderLayer
{
	Background = -1,
	World = 0,
	Ui = 1
}

public class Entity
{
	public Level Level { get; set; }
	public static List<Entity> All { get; set; } = Assembly.GetCallingAssembly().GetTypes().OfType<Entity>().ToList();

	/// <summary>
	/// Which render pass draws this entity. Defaults to the 3D world.
	/// </summary>
	public RenderLayer Layer { get; protected set; } = RenderLayer.World;

	/// <summary>
	/// Returns <paramref name="entities"/> in draw order: grouped by <see cref="RenderLayer"/>
	/// (World before Ui), keeping creation order inside each layer. The result is a snapshot,
	/// so entities spawned while rendering do not disturb the current frame.
	/// </summary>
	public static List<Entity> InRenderOrder( IEnumerable<Entity> entities )
	{
		// OrderBy is a stable sort, which preserves creation order within a layer.
		return entities.OrderBy( entity => entity.Layer ).ToList();
	}

	/// <summary>
	/// Right, Up, Forward (FLU)
	/// </summary>
	public Vector3 Position;

	/// <summary>
	/// Pitch, Yaw, Roll (PYR)
	/// </summary>
	public Quaternion Rotation;

	public Vector3 Scale = Vector3.One;

	public string Name { get; set; }

	public Matrix4x4 ModelMatrix
	{
		get
		{
			var matrix = Matrix4x4.CreateScale( Scale.GetSystemVector3() );
			matrix *= Matrix4x4.CreateFromQuaternion( Rotation );
			matrix *= Matrix4x4.CreateTranslation( Position.GetSystemVector3() );

			return matrix;
		}
	}

	public Entity()
	{
		Level = Level.Current;
		All.Add( this );
		Name = $"{this.GetType().Name} {All.Count}";
	}

	public void Render()
	{
		OnRender();
	}

	public void Update()
	{
		OnUpdate();
	}
	public void Delete()
	{
		OnDelete();
	}

	protected virtual void OnRender() { }
	protected virtual void OnUpdate() { }
	protected virtual void OnDelete() { }

	public bool Equals( Entity x, Entity y ) => x.GetHashCode() == y.GetHashCode();
	public int GetHashCode( [DisallowNull] Entity obj ) => base.GetHashCode();
}
