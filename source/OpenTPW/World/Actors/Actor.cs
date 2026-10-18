using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;

namespace OpenTPW;

public class Actor
{
	public LobbyScene Level { get; set; }
	public static List<Actor> All { get; set; } = Assembly.GetCallingAssembly().GetTypes().OfType<Actor>().ToList();

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
		get => Transform.CreateMatrix( Position, Rotation, Scale );
	}

	internal static void UpdateAll() => ForEachSnapshot( static actor => actor.Update() );
	internal static void RenderAll() => ForEachSnapshot( static actor => actor.Render() );
	internal static void RenderUIAll() => ForEachSnapshot( static actor => actor.RenderUI() );
	internal static void DeleteAll() => ForEachSnapshot( static actor => actor.Delete() );

	private static void ForEachSnapshot( Action<Actor> callback )
	{
		// Callbacks may add or remove actors. Every scene uses the same snapshot rule.
		foreach ( var actor in All.ToArray() )
		{
			callback( actor );
		}
	}

	public Actor()
	{
		Level = LobbyScene.Current;
		All.Add( this );
		Name = $"{this.GetType().Name} {All.Count}";
	}

	public void Render()
	{
		OnRender();
	}

	public void RenderUI()
	{
		OnRenderUI();
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
	protected virtual void OnRenderUI() { }
	protected virtual void OnUpdate() { }
	protected virtual void OnDelete() { }

	public bool Equals( Actor x, Actor y ) => x.GetHashCode() == y.GetHashCode();
	public int GetHashCode( [DisallowNull] Actor obj ) => base.GetHashCode();
}
