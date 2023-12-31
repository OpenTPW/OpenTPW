﻿using System.Numerics;
using Veldrid;

namespace OpenTPW.UI;

public partial class Image : Panel
{
	private Model model;

	struct ObjectUniformBuffer
	{
		public Matrix4x4 g_mModel;
	}

	public Image( string texturePath )
	{
		var textureFile = new TextureFile( GameDir.GetPath( texturePath ) );

		var material = new Material(
			new Texture( textureFile ),
			ShaderBuilder.Default.WithVertex( "content/shaders/test.vert" )
						  .WithFragment( "content/shaders/test.frag" )
						  .Build(),
			typeof( ObjectUniformBuffer )
		);

		model = Primitives.Plane.GenerateModel( material );
	}

	public Image( Texture texture )
	{
		var material = new Material(
			texture,
			ShaderBuilder.Default.WithVertex( "content/shaders/test.vert" )
						  .WithFragment( "content/shaders/test.frag" )
						  .Build(),
			typeof( ObjectUniformBuffer )
		);

		model = Primitives.Plane.GenerateModel( material );
	}

	public override void Update()
	{
		base.Update();

		position = new Vector2( Screen.Size.X / 2, Screen.Size.Y / 2 );
		size = new Vector2( Screen.Size.X, Screen.Size.Y );
	}

	public override void Draw( CommandList commandList )
	{
		base.Draw( commandList );

		var uniformBufferContents = new ObjectUniformBuffer
		{
			g_mModel = modelMatrix
		};

		model.Draw( uniformBufferContents, commandList );
	}
}
