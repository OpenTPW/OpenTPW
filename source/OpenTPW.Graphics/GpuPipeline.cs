using SDL;
using static SDL.SDL3;

namespace OpenTPW.Graphics;

/// <summary>Colour blending; the factors match OpenTPW's MaterialFlags (Material.BlendState).</summary>
public enum GpuBlend
{
	/// <summary>No blending: the fragment replaces the target.</summary>
	None,
	/// <summary>SrcAlpha / 1-SrcAlpha for colour and alpha (Veldrid SingleAlphaBlend, OpenTPW's default).</summary>
	Alpha,
	/// <summary>SrcAlpha / One for colour and alpha (MaterialFlags.Additive).</summary>
	Additive,
	/// <summary>1-DstColor / Zero and 1-DstAlpha / Zero (MaterialFlags.InvertDestination).</summary>
	InvertDestination,
}

public enum GpuCull { None, Back, Front }

/// <summary>One vertex input attribute: shader location, element format and byte offset within the vertex.</summary>
public readonly record struct GpuVertexAttribute( uint Location, SDL_GPUVertexElementFormat Format, uint Offset );

/// <summary>A single interleaved vertex buffer layout, compared by content so it can be part of a pipeline key.</summary>
public sealed class GpuVertexLayout( uint stride, params GpuVertexAttribute[] attributes ) : IEquatable<GpuVertexLayout>
{
	public uint Stride { get; } = stride;
	public IReadOnlyList<GpuVertexAttribute> Attributes { get; } = attributes.ToArray();
	/// <summary>No vertex buffer: positions come from SV_VertexID (the blit shader).</summary>
	public static GpuVertexLayout Empty { get; } = new( 0 );

	public bool Equals( GpuVertexLayout? other ) => other != null && Stride == other.Stride && Attributes.SequenceEqual( other.Attributes );
	public override bool Equals( object? obj ) => Equals( obj as GpuVertexLayout );
	public override int GetHashCode() => Attributes.Aggregate( (int)Stride, ( hash, attribute ) => HashCode.Combine( hash, attribute ) );
}

/// <summary>
/// Everything a graphics pipeline is built from; equal descriptions share one cached pipeline
/// (<see cref="GpuDevice.GetPipeline"/>). Defaults reproduce an OpenTPW material without flags.
/// </summary>
public sealed record GpuPipelineDescription( ShaderProgram Program, GpuVertexLayout VertexLayout,
	SDL_GPUTextureFormat ColorFormat, SDL_GPUTextureFormat? DepthFormat, SDL_GPUSampleCount SampleCount = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1 )
{
	public GpuBlend Blend { get; init; } = GpuBlend.Alpha;
	public bool DepthTest { get; init; } = true;
	public bool DepthWrite { get; init; } = true;
	public SDL_GPUCompareOp DepthCompare { get; init; } = SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS;
	public GpuCull Cull { get; init; } = GpuCull.Back;
	public SDL_GPUFrontFace FrontFace { get; init; } = SDL_GPUFrontFace.SDL_GPU_FRONTFACE_CLOCKWISE;
	public SDL_GPUPrimitiveType Primitive { get; init; } = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST;

	/// <summary>A description for drawing into <paramref name="target"/>, taking its formats and sample count.</summary>
	public static GpuPipelineDescription For( RenderTarget target, ShaderProgram program, GpuVertexLayout layout )
		=> new( program, layout, target.ColorFormat, target.DepthFormat, target.SampleCount );
}

/// <summary>A compiled graphics pipeline, owned and cached by its device.</summary>
public sealed unsafe class GpuPipeline
{
	private readonly GpuDevice device;
	internal SDL_GPUGraphicsPipeline* Handle { get; private set; }
	public GpuPipelineDescription Description { get; }

	internal GpuPipeline( GpuDevice device, GpuPipelineDescription description )
	{
		this.device = device;
		Description = description;
		var layout = description.VertexLayout;
		var attributes = layout.Attributes.Select( attribute => new SDL_GPUVertexAttribute
		{
			location = attribute.Location, buffer_slot = 0, format = attribute.Format, offset = attribute.Offset
		} ).ToArray();
		var buffer = new SDL_GPUVertexBufferDescription
		{
			slot = 0, pitch = layout.Stride, input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX
		};
		var color = new SDL_GPUColorTargetDescription { format = description.ColorFormat, blend_state = BlendState( description.Blend ) };
		bool depth = description.DepthFormat.HasValue;
		fixed ( SDL_GPUVertexAttribute* attributePointer = attributes )
		{
			var info = new SDL_GPUGraphicsPipelineCreateInfo
			{
				vertex_shader = description.Program.Vertex,
				fragment_shader = description.Program.Fragment,
				vertex_input_state = new SDL_GPUVertexInputState
				{
					vertex_buffer_descriptions = layout.Stride == 0 ? null : &buffer,
					num_vertex_buffers = layout.Stride == 0 ? 0u : 1u,
					vertex_attributes = attributePointer,
					num_vertex_attributes = (uint)attributes.Length
				},
				primitive_type = description.Primitive,
				rasterizer_state = new SDL_GPURasterizerState
				{
					fill_mode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL,
					cull_mode = description.Cull switch
					{
						GpuCull.Back => SDL_GPUCullMode.SDL_GPU_CULLMODE_BACK,
						GpuCull.Front => SDL_GPUCullMode.SDL_GPU_CULLMODE_FRONT,
						_ => SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE
					},
					front_face = description.FrontFace,
					enable_depth_clip = true
				},
				multisample_state = new SDL_GPUMultisampleState { sample_count = description.SampleCount },
				depth_stencil_state = new SDL_GPUDepthStencilState
				{
					enable_depth_test = depth && description.DepthTest,
					enable_depth_write = depth && description.DepthWrite,
					compare_op = description.DepthCompare
				},
				target_info = new SDL_GPUGraphicsPipelineTargetInfo
				{
					color_target_descriptions = &color,
					num_color_targets = 1,
					has_depth_stencil_target = depth,
					depth_stencil_format = description.DepthFormat ?? SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_INVALID
				}
			};
			Handle = SDL_CreateGPUGraphicsPipeline( device.Handle, &info );
		}
		if ( Handle == null ) throw GraphicsException.FromSdl( nameof(SDL_CreateGPUGraphicsPipeline) );
	}

	internal static SDL_GPUColorTargetBlendState BlendState( GpuBlend blend )
	{
		var (sourceColor, destinationColor, sourceAlpha, destinationAlpha) = blend switch
		{
			GpuBlend.Alpha => (SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA, SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
				SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA, SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA),
			GpuBlend.Additive => (SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA, SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE,
				SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA, SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE),
			GpuBlend.InvertDestination => (SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_DST_COLOR, SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ZERO,
				SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_DST_ALPHA, SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ZERO),
			_ => (SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE, SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ZERO,
				SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE, SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ZERO),
		};
		return new SDL_GPUColorTargetBlendState
		{
			enable_blend = blend != GpuBlend.None,
			src_color_blendfactor = sourceColor,
			dst_color_blendfactor = destinationColor,
			color_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,
			src_alpha_blendfactor = sourceAlpha,
			dst_alpha_blendfactor = destinationAlpha,
			alpha_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD
		};
	}

	internal void Destroy()
	{
		if ( Handle != null ) SDL_ReleaseGPUGraphicsPipeline( device.Handle, Handle );
		Handle = null;
	}
}
