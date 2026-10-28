using System.Diagnostics;
using Veldrid;

namespace OpenTPW;

[Flags]
public enum MaterialFlags
{
	None,

	DisableDepthTest,
	DisableDepthWrite,

	DisableDepth = DisableDepthTest | DisableDepthWrite,
	DisableCulling = 4,
	Additive = 8,
	InvertDestination = 16
}

public partial class Material : Asset
{
	public Shader Shader { get; set; }

	public Type UniformBufferType { get; } = typeof( ObjectUniformBuffer );
	public Pipeline Pipeline { get; private set; } = null!;

	private Dictionary<string, BindableResource> _boundResources = new();

	private ResourceLayout[] _resourceLayouts;

	public Material( string shaderPath, MaterialFlags flags = MaterialFlags.None )
	{
		Shader = new Shader( shaderPath );
		Shader.OnRecompile += () => SetupResources( flags );

		All.Add( this );
		SetupResources( flags );
	}

	protected Material( string shaderPath, Type uniformBufferType, MaterialFlags flags = MaterialFlags.None )
		: this( new Shader( shaderPath ), uniformBufferType, flags )
	{
	}

	protected Material( Shader shader, Type uniformBufferType, MaterialFlags flags = MaterialFlags.None )
	{
		Shader = shader;
		Shader.OnRecompile += () => SetupResources( flags );
		UniformBufferType = uniformBufferType;

		All.Add( this );
		SetupResources( flags );
	}

	// Resource sets keyed by the exact resources bound. Frame buffers are reused in the same order every
	// frame, so the same combinations recur and sets need not be created and destroyed per draw.
	private readonly Dictionary<ResourceKey, ResourceSet[]> resourceSets = new();

	private sealed class ResourceKey( BindableResource[] resources ) : IEquatable<ResourceKey>
	{
		private readonly BindableResource[] resources = resources;
		private readonly int hash = resources.Aggregate( 17, ( h, r ) => h * 31 + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode( r ) );
		public bool Equals( ResourceKey? other ) => other != null && other.resources.Length == resources.Length
			&& resources.Zip( other.resources ).All( pair => ReferenceEquals( pair.First, pair.Second ) );
		public override bool Equals( object? obj ) => Equals( obj as ResourceKey );
		public override int GetHashCode() => hash;
	}

	private static readonly Sampler[] Samplers =
	[
		CreateSampler( SamplerType.Anisotropic ),
		CreateSampler( SamplerType.Linear ),
		CreateSampler( SamplerType.Point ),
		CreateSampler( SamplerType.AnisotropicWrap ),
		CreateSampler( SamplerType.AnisotropicRepeat ),
		CreateSampler( SamplerType.PointWrap ),
	];

	private static Sampler CreateSampler( SamplerType type )
	{
		var samplerFilter = type switch
		{
			SamplerType.Anisotropic or SamplerType.AnisotropicWrap or SamplerType.AnisotropicRepeat => SamplerFilter.Anisotropic,
			SamplerType.Linear => SamplerFilter.MinLinear_MagLinear_MipLinear,
			SamplerType.Point or SamplerType.PointWrap => SamplerFilter.MinPoint_MagPoint_MipPoint,
			_ => throw new NotImplementedException()
		};

		var samplerAddressMode = type switch
		{
			SamplerType.Anisotropic or SamplerType.Linear or SamplerType.Point => SamplerAddressMode.Clamp,
			SamplerType.AnisotropicWrap or SamplerType.PointWrap => SamplerAddressMode.Wrap,
			SamplerType.AnisotropicRepeat => SamplerAddressMode.Mirror,
			_ => throw new NotImplementedException()
		};

		var samplerDescription = new SamplerDescription(
			samplerAddressMode,
			samplerAddressMode,
			samplerAddressMode,
			samplerFilter,
			ComparisonKind.Always,
			(type == SamplerType.Anisotropic || type == SamplerType.AnisotropicWrap || type == SamplerType.AnisotropicRepeat) ? 16u : 0u,
			0,
			10,
			0,
			SamplerBorderColor.TransparentBlack
		);

		return Device.ResourceFactory.CreateSampler( samplerDescription );
	}

	public void Set<T>( string name, T obj ) where T : unmanaged
	{
		Render.BufferUploads++;
		// Each draw gets its own frame buffer: draws record now but execute after the whole frame is
		// recorded, so a buffer shared by every draw of this material would hold only the last value.
		var buffer = Render.RentFrameBuffer( BufferUsage.UniformBuffer, (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<T>() );
		Renderer.Write<T>( buffer, [obj] );
		_boundResources[name] = buffer;
	}

	public void Set( string name, Texture[] texture, SamplerType sampler = SamplerType.AnisotropicWrap )
	{
		for ( int i = 0; i < texture.Length; i++ )
		{
			_boundResources[name + $"{i}"] = texture[i].NativeTexture;
		}

		_boundResources["s_" + name] = Samplers[(int)sampler];
	}

	public void Set( string name, Texture texture, SamplerType? sampler = null )
	{
		_boundResources[name] = texture.NativeTexture;
		_boundResources["s_" + name] = Samplers[(int)(sampler ?? texture.SamplerType)];
	}

	internal ResourceLayout[] CreateResourceLayouts()
	{
		return Shader.ResourceLayouts.Select( x => Device.ResourceFactory.CreateResourceLayout( x ) ).ToArray();
	}

	internal ResourceSet[] CreateResourceSets()
	{
		Debug.Assert( _resourceLayouts != null );

		List<ResourceSetDescription> resourceSetDescriptions = new();

		for ( int i = 0; i < Shader.ResourceLayouts.Length; i++ )
		{
			ResourceLayoutDescription resourceLayout = Shader.ResourceLayouts[i];
			var sortedBoundResources = new List<BindableResource>();

			foreach ( var resource in resourceLayout.Elements )
			{
				if ( _boundResources.TryGetValue( resource.Name, out var boundResource ) )
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
				Layout = _resourceLayouts[i],
				BoundResources = [.. sortedBoundResources]
			};

			resourceSetDescriptions.Add( resourceSetDescription );
		}

		return resourceSetDescriptions.Select( x => Device.ResourceFactory.CreateResourceSet( x ) ).ToArray();
	}

	/// <summary>Resource sets for the resources bound now, created once per combination.</summary>
	internal ResourceSet[] GetResourceSets()
	{
		var bound = Shader.ResourceLayouts.SelectMany( layout => layout.Elements ).Select( element =>
			_boundResources.TryGetValue( element.Name, out var resource ) ? resource
				: throw new Exception( $"{element.Name} wasn't bound at draw time!" ) ).ToArray();
		var key = new ResourceKey( bound );
		if ( resourceSets.TryGetValue( key, out var sets ) ) return sets;
		if ( resourceSets.Count >= 256 )
		{
			// A changing draw order can produce new combinations; release old ones after this frame.
			var stale = resourceSets.Values.ToArray();
			resourceSets.Clear();
			Render.ScheduleDelete( () => { foreach ( var item in stale ) DestroyResourceSets( item ); } );
		}
		resourceSets.Add( key, sets = CreateResourceSets() );
		return sets;
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

	private void SetupResources( MaterialFlags flags )
	{
		var vertexLayout = new VertexLayoutDescription( Vertex.VertexElementDescriptions );

		//
		// Create resource layout - but only from what we're using/need
		//
		_resourceLayouts ??= CreateResourceLayouts();

		//
		// Create pipeline
		//
		var pipelineDescription = new GraphicsPipelineDescription()
		{
			BlendState = BlendState( flags ),

			DepthStencilState = new DepthStencilStateDescription(
				!flags.HasFlag( MaterialFlags.DisableDepthTest ),
				!flags.HasFlag( MaterialFlags.DisableDepthWrite ),
				flags.HasFlag( MaterialFlags.DisableDepthTest | MaterialFlags.DisableDepthWrite ) ? ComparisonKind.Always : ComparisonKind.Less
			),

			RasterizerState = new RasterizerStateDescription(
				flags.HasFlag( MaterialFlags.DisableCulling ) ? FaceCullMode.None : FaceCullMode.Back,
				PolygonFillMode.Solid,
				FrontFace.Clockwise,
				true,
				false
			),

			PrimitiveTopology = PrimitiveTopology.TriangleList,
			ResourceLayouts = [.. _resourceLayouts],
			ShaderSet = new ShaderSetDescription( [vertexLayout], Shader.ShaderProgram ),
			Outputs = Render.MultisampledFramebuffer.OutputDescription
		};

		Pipeline = Device.ResourceFactory.CreateGraphicsPipeline( pipelineDescription );
	}

	internal static BlendStateDescription BlendState( MaterialFlags flags ) => (flags & (MaterialFlags.Additive | MaterialFlags.InvertDestination)) != 0
		? new BlendStateDescription
		{
			BlendFactor = RgbaFloat.Black,
			AttachmentStates = [new BlendAttachmentDescription
			{
				BlendEnabled = true,
				SourceColorFactor = flags.HasFlag( MaterialFlags.InvertDestination ) ? BlendFactor.InverseDestinationColor : BlendFactor.SourceAlpha,
				DestinationColorFactor = flags.HasFlag( MaterialFlags.InvertDestination ) ? BlendFactor.Zero : BlendFactor.One,
				ColorFunction = BlendFunction.Add,
				SourceAlphaFactor = flags.HasFlag( MaterialFlags.InvertDestination ) ? BlendFactor.InverseDestinationAlpha : BlendFactor.SourceAlpha,
				DestinationAlphaFactor = flags.HasFlag( MaterialFlags.InvertDestination ) ? BlendFactor.Zero : BlendFactor.One,
				AlphaFunction = BlendFunction.Add
			}]
		} : BlendStateDescription.SingleAlphaBlend;
}

public class Material<T> : Material
{
	public Material( string shaderPath, MaterialFlags flags = MaterialFlags.None ) : base( shaderPath, typeof( T ), flags ) { }
	public Material( Shader shader, MaterialFlags flags = MaterialFlags.None ) : base( shader, typeof( T ), flags ) { }
}
