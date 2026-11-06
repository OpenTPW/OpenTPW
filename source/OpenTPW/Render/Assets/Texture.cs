using StbImageSharp;
using OpenTPW.Graphics;

namespace OpenTPW;

public partial class Texture : Asset
{
	public SamplerType SamplerType { get; private set; } = SamplerType.AnisotropicRepeat;

	public uint Width { get; private set; }
	public uint Height { get; private set; }

	public static Texture Missing => new Texture( [255, 255, 255, 255], 1, 1 );

	internal GpuTexture NativeTexture = null!;

	/// <summary>
	/// From path on disk, or from a game resource
	/// </summary>
	public Texture( string path, TextureFlags flags = TextureFlags.None )
	{
		if ( path.HasExtension( ".wct" ) )
			UpdateFromWct( path, flags );
		else
			UpdateFromStb( path, flags );
	}

	/// <summary>
	/// From data as bytes
	/// </summary>
	public Texture( byte[] data, int width, int height, TextureFlags flags = TextureFlags.None )
	{
		CreateTexture( "", data, (uint)width, (uint)height, flags );
	}

	public Texture( Stream stream, TextureFlags flags = TextureFlags.None )
	{
		var fileData = new byte[stream.Length];
		stream.ReadExactly( fileData, 0, fileData.Length );

		var image = ImageResult.FromMemory( fileData, ColorComponents.RedGreenBlueAlpha );

		var data = image.Data;
		var width = (uint)image.Width;
		var height = (uint)image.Height;
		var debugName = $"Stream {stream.GetHashCode()}";

		CreateTexture( debugName, data, width, height, flags );
	}

	/// <summary>
	/// Update from an image format supported by the STB library (jpg, png, gif, tga, etc.)
	/// </summary>
	private void UpdateFromStb( string path, TextureFlags flags )
	{
		var fileData = File.ReadAllBytes( path );
		var image = ImageResult.FromMemory( fileData, ColorComponents.RedGreenBlueAlpha );

		var data = image.Data;
		var width = (uint)image.Width;
		var height = (uint)image.Height;

		CreateTexture( path, data, width, height, flags );
	}

	/// <summary>
	/// Update from a Bullfrog WCT file
	/// </summary>
	private void UpdateFromWct( string path, TextureFlags flags )
	{
		var textureFileData = new TextureFile( path ).Data;

		CreateTexture( path, textureFileData.Data, (uint)textureFileData.Width, (uint)textureFileData.Height, flags );
	}

	private void PreprocessTextureData( ref byte[] data, ref uint width, ref uint height, TextureFlags flags )
	{
		if ( flags == TextureFlags.None )
			return;

		if ( flags.HasFlag( TextureFlags.PinkChromaKey ) )
		{
			for ( int i = 0; i < data.Length; i += 4 )
			{
				var r = data[i];
				var g = data[i + 1];
				var b = data[i + 2];

				if ( r == 255 && g == 0 && b == 255 )
				{
					// Set alpha to 0
					data[i + 3] = 0;
				}
			}
		}
	}

	private void CreateTexture( string debugName, byte[] data, uint width, uint height, TextureFlags flags )
	{
		if ( TryGetCachedTexture( debugName, flags, out var cachedTexture ) )
		{
			NativeTexture = cachedTexture!.NativeTexture;
			// A wrapper still needs its own metadata, even though the GPU allocation is shared.
			Path = cachedTexture.Path;
			Width = cachedTexture.Width;
			Height = cachedTexture.Height;
			SamplerType = cachedTexture.SamplerType;
			cacheFlags = flags;

			return;
		}

		PreprocessTextureData( ref data, ref width, ref height, flags );
		// Every texture goes through the retail 16-bit texture format (RetailTexturePixels.QuantizeArgb4444).
		// Copy first: callers may keep reusing their pixel buffer.
		data = (byte[])data.Clone();
		RetailTexturePixels.QuantizeArgb4444( data );

		if ( flags.HasFlag( TextureFlags.PointFilter ) )
			SamplerType = SamplerType.Point;

		if ( flags.HasFlag( TextureFlags.Wrap ) )
			SamplerType = SamplerType.AnisotropicWrap;

		if ( flags.HasFlag( TextureFlags.Repeat ) )
			SamplerType = SamplerType.AnisotropicRepeat;

		Path = debugName;
		cacheFlags = flags;
		NativeTexture = TexturePixels.Create( Device, data, width, height );
		Width = width;
		Height = height;

		All.Add( this );
	}
}
