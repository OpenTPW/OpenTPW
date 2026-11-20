// The GLSL sources share one sampler across sixteen independent textures. SDL
// GPU pairs texture and sampler at each slot: bind the same sampler in all slots.
TEXTURE_SLOT( Color0, 0 );
TEXTURE_SLOT( Color1, 1 );
TEXTURE_SLOT( Color2, 2 );
TEXTURE_SLOT( Color3, 3 );
TEXTURE_SLOT( Color4, 4 );
TEXTURE_SLOT( Color5, 5 );
TEXTURE_SLOT( Color6, 6 );
TEXTURE_SLOT( Color7, 7 );
TEXTURE_SLOT( Color8, 8 );
TEXTURE_SLOT( Color9, 9 );
TEXTURE_SLOT( Color10, 10 );
TEXTURE_SLOT( Color11, 11 );
TEXTURE_SLOT( Color12, 12 );
TEXTURE_SLOT( Color13, 13 );
TEXTURE_SLOT( Color14, 14 );
TEXTURE_SLOT( Color15, 15 );

float4 SampleModel( int texIndex, float2 uv )
{
	float4 result = float4( 1, 0, 1, 1 );
	if ( texIndex == 0 ) result = Color0.Sample( Color0Sampler, uv );
	if ( texIndex == 1 ) result = Color1.Sample( Color1Sampler, uv );
	if ( texIndex == 2 ) result = Color2.Sample( Color2Sampler, uv );
	if ( texIndex == 3 ) result = Color3.Sample( Color3Sampler, uv );
	if ( texIndex == 4 ) result = Color4.Sample( Color4Sampler, uv );
	if ( texIndex == 5 ) result = Color5.Sample( Color5Sampler, uv );
	if ( texIndex == 6 ) result = Color6.Sample( Color6Sampler, uv );
	if ( texIndex == 7 ) result = Color7.Sample( Color7Sampler, uv );
	if ( texIndex == 8 ) result = Color8.Sample( Color8Sampler, uv );
	if ( texIndex == 9 ) result = Color9.Sample( Color9Sampler, uv );
	if ( texIndex == 10 ) result = Color10.Sample( Color10Sampler, uv );
	if ( texIndex == 11 ) result = Color11.Sample( Color11Sampler, uv );
	if ( texIndex == 12 ) result = Color12.Sample( Color12Sampler, uv );
	if ( texIndex == 13 ) result = Color13.Sample( Color13Sampler, uv );
	if ( texIndex == 14 ) result = Color14.Sample( Color14Sampler, uv );
	if ( texIndex == 15 ) result = Color15.Sample( Color15Sampler, uv );
	return result;
}
