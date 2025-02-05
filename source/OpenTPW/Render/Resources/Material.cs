using System.Diagnostics;
using System.Linq;
using Veldrid;
using static ICSharpCode.SharpZipLib.Zip.ExtendedUnixData;

namespace OpenTPW;

[Flags]
public enum MaterialFlags
{
	None,

	DisableDepthTest,
	DisableDepthWrite,

	DisableDepth = DisableDepthTest | DisableDepthWrite
}

public partial class Material : Resource
{
	public Shader Shader { get; set; }

	public Type UniformBufferType { get; } = typeof( ObjectUniformBuffer );
	public Pipeline Pipeline { get; private set; } = null!;
	public ResourceBinding Resources { get; } = new();

	private MaterialFlags _creationFlags;

	public Material( string shaderPath, MaterialFlags flags = MaterialFlags.None )
	{
		_creationFlags = flags;

		Shader = new Shader( shaderPath );
		Shader.OnRecompile += () => SetupResources( flags );
		SetupResources( flags );

		All.Add( this );
	}

	protected Material( string shaderPath, Type uniformBufferType, MaterialFlags flags = MaterialFlags.None )
	{
		_creationFlags = flags;

		Shader = new Shader( shaderPath );
		Shader.OnRecompile += () => SetupResources( flags );
		UniformBufferType = uniformBufferType;
		SetupResources( flags );

		All.Add( this );
	}

	private void SetupResources( MaterialFlags flags )
	{
		var vertexLayout = new VertexLayoutDescription( Vertex.VertexElementDescriptions );

		//
		// Create pipeline
		//
		var pipelineDescription = new GraphicsPipelineDescription()
		{
			BlendState = BlendStateDescription.SingleAlphaBlend,

			DepthStencilState = new DepthStencilStateDescription(
				!flags.HasFlag( MaterialFlags.DisableDepthTest ),
				!flags.HasFlag( MaterialFlags.DisableDepthWrite ),
				flags.HasFlag( MaterialFlags.DisableDepthTest | MaterialFlags.DisableDepthWrite ) ? ComparisonKind.Always : ComparisonKind.Less
			),

			RasterizerState = new RasterizerStateDescription(
				flags.HasFlag( MaterialFlags.DisableDepth ) ? FaceCullMode.None : FaceCullMode.Back,
				PolygonFillMode.Solid,
				FrontFace.Clockwise,
				true,
				false
			),

			PrimitiveTopology = PrimitiveTopology.TriangleList,
			ResourceLayouts = [.. Shader.ResourceLayouts],
			ShaderSet = new ShaderSetDescription( [vertexLayout], Shader.ShaderProgram ),
			Outputs = Render.MultisampledFramebuffer.OutputDescription
		};

		Pipeline = Device.ResourceFactory.CreateGraphicsPipeline( pipelineDescription );
	}

	private Material( Type uniformBufferType, MaterialFlags flags )
	{
		_creationFlags = flags;
	}

	private static void DestroyResourceSets( ResourceSet[] resourceSets )
	{
		if ( resourceSets == null )
		{
			Log.Warning( $"Resource sets were marked for death, but are already dead - we can't kill what's already dead!" );
			return;
		}

		foreach ( var item in resourceSets )
		{
			item.Dispose();
		}
	}

	public void Set<T>( string name, T obj ) where T : unmanaged => Resources.Set( name, obj );
	public void Set( string name, Texture[] textures ) => Resources.Set( name, textures );
	public void Set( string name, Texture texture ) => Resources.Set( name, texture );

	private Dictionary<string, BindableResource> GetBoundResources()
	{
		Dictionary<string, BindableResource> boundResources = [];

		void ProcessResources( ResourceBinding resources )
		{
			foreach ( var pair in resources.BoundResources )
			{
				if ( boundResources.ContainsKey( pair.Key ) )
					continue;

				boundResources[pair.Key] = pair.Value;
			}
		}

		//
		//
		//

		foreach ( var item in ResourceContext.Stack )
		{
			ProcessResources( item.Resources );
		}

		ProcessResources( Resources );

		return boundResources;
	}

	private ResourceSet[] CreateResourceSets()
	{
		var boundResources = GetBoundResources();
		var resourceLayouts = Shader.ResourceLayouts;

		List<ResourceSetDescription> resourceSetDescriptions = new();

		for ( int i = 0; i < Shader.ResourceLayouts.Length; i++ )
		{
			var layoutDesc = Shader.ResourceLayoutDescriptions[i];
			var sortedBoundResources = new List<BindableResource>();

			foreach ( var resource in layoutDesc.Elements )
			{
				if ( boundResources.TryGetValue( resource.Name, out var boundResource ) )
				{
					sortedBoundResources.Add( boundResource );
				}
				else
				{
					throw new Exception( $"{resource.Name} wasn't bound at draw time!" );
				}
			}

			var resourceSetDescription = new ResourceSetDescription()
			{
				Layout = resourceLayouts[i],
				BoundResources = [.. sortedBoundResources]
			};

			resourceSetDescriptions.Add( resourceSetDescription );
		}

		return resourceSetDescriptions.Select( x => Device.ResourceFactory.CreateResourceSet( x ) ).ToArray();
	}

	public void GetTemporaryResourceSet( out ResourceSet[] resourceSets )
	{
		// Create a set
		var newResourceSets = CreateResourceSets();
		resourceSets = newResourceSets;

		// Mark set for deletion
		Render.ScheduleDelete( () => DestroyResourceSets( newResourceSets ) );
	}

	/// <summary>
	/// Creates a deep copy of this material
	/// </summary>
	public Material Copy()
	{
		var copy = new Material( UniformBufferType, _creationFlags )
		{
			Shader = Shader
		};

		copy.Shader.OnRecompile += () => copy.SetupResources( _creationFlags );
		copy.SetupResources( _creationFlags );

		return copy;
	}
}

public class Material<T>( string shaderPath, MaterialFlags flags = MaterialFlags.None ) : Material( shaderPath, typeof( T ), flags )
{
	/// <summary>
	/// Creates a typed copy of this material
	/// </summary>
	public new Material<T> Copy()
	{
		var copy = (Material<T>)base.Copy();
		return copy;
	}
}
