using static SDL.SDL3;

namespace OpenTPW.Graphics;

/// <summary>An SDL GPU call failed; the message carries SDL's error text.</summary>
public sealed class GraphicsException( string message ) : Exception( message )
{
	internal static GraphicsException FromSdl( string call ) => new( $"{call} failed: {SDL_GetError()}" );

	internal static void Check( SDL.SDLBool success, string call )
	{
		if ( !success ) throw FromSdl( call );
	}
}
