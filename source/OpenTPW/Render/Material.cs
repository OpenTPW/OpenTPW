﻿using Veldrid;

namespace OpenTPW;

public struct Material
{
	public Shader Shader { get; set; }
	public Type UniformBufferType { get; }
	public Texture DiffuseTexture { get; set; }

	public static Material Default => new Material(
			TextureBuilder.WorldTexture.FromPath( "content/textures/test.png" ).Build(),
			ShaderBuilder.Default.WithVertex( "content/shaders/3d/3d.vert" )
							 .WithFragment( "content/shaders/3d/3d.frag" )
							 .Build(),
			typeof( ObjectUniformBuffer )
		);

	/// <summary>
	/// Create a material with the default shader and uniform buffer type.
	/// </summary>
	public Material( Texture diffuseTexture )
	{
		DiffuseTexture = diffuseTexture;
		Shader = ShaderBuilder.Default.WithVertex( "content/shaders/3d/3d.vert" )
								.WithFragment( "content/shaders/3d/3d.frag" )
								.Build();
		UniformBufferType = typeof( ObjectUniformBuffer );
	}

	public Material( Texture diffuseTexture, Shader shader, Type uniformBufferType )
	{
		DiffuseTexture = diffuseTexture;
		Shader = shader;
		UniformBufferType = uniformBufferType;
	}

	public bool IsDirty => DiffuseTexture.IsDirty;

	public void GenerateMipmaps( CommandList commandList )
	{
		if ( DiffuseTexture.IsDirty )
			DiffuseTexture.GenerateMipmaps( commandList );
	}
}
