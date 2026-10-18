using System;
using System.Collections.Generic;
using System.IO;

namespace OpenTPW;

/// <summary>Version 3 indexed TPC frames, as used by Generic/Particles/SPR_PA.TPC.</summary>
public static class TpcSpriteBank
{
	private const ushort FormatVersion = 3;
	private const ushort IndexedFrameFormat = 3;
	private const int PaletteEntries = 256;
	private const int ColorChannels = 4;
	private const int PaletteBytes = PaletteEntries * ColorChannels;
	// Defensive bounds for untrusted frame sizes and allocations.
	private const uint MaximumFrameCount = 4096;
	private const int MaximumFrameDimension = 4096;
	private const uint MaximumPayloadBytes = 16 * 1024 * 1024;
	public static IReadOnlyList<TextureData> Read( Stream stream )
	{
		using var reader = new BinaryReader( stream, System.Text.Encoding.ASCII, true );
		if ( reader.ReadUInt16() != FormatVersion || reader.ReadUInt16() != IndexedFrameFormat )
		{
			throw new InvalidDataException( "Unsupported TPC sprite-bank format." );
		}

		uint count = reader.ReadUInt32();
		if ( count > MaximumFrameCount )
		{
			throw new InvalidDataException( "TPC frame count exceeds the supported limit." );
		}

		byte[] palette = reader.ReadExactBytes( PaletteBytes );

		var frames = new List<TextureData>();
		for ( uint frame = 0; frame < count; frame++ )
		{
			frames.Add( ReadFrame( reader, palette ) );
		}

		return frames;
	}

	private static TextureData ReadFrame( BinaryReader reader, byte[] palette )
	{
		uint length = reader.ReadUInt32();
		int width = reader.ReadUInt16(), height = reader.ReadUInt16();
		_ = reader.ReadUInt16(); // Logical width.
		_ = reader.ReadUInt16(); // Logical height.
		_ = reader.ReadInt32(); // Pivot X.
		_ = reader.ReadInt32(); // Pivot Y.
		if ( width == 0 || height == 0 || width > MaximumFrameDimension || height > MaximumFrameDimension || length > MaximumPayloadBytes )
		{
			throw new InvalidDataException( "Invalid TPC frame dimensions or payload length." );
		}

		byte[] encoded = reader.ReadExactBytes( (int)length );

		return DecodeFrame( encoded, palette, width, height );
	}

	private static TextureData DecodeFrame( byte[] encoded, byte[] palette, int width, int height )
	{
		var rgba = new byte[checked( width * height * ColorChannels )];
		int cursor = 0;
		for ( int y = 0; y < height; y++ )
		{
			if ( cursor >= encoded.Length )
			{
				throw new InvalidDataException( "Missing TPC scanline." );
			}

			// Each row starts with its encoded byte count. Positive runs carry
			// literal palette indices; negative runs repeat the next index.
			int end = cursor + 1 + encoded[cursor];
			cursor++;
			if ( end > encoded.Length )
			{
				throw new InvalidDataException( "Truncated TPC scanline." );
			}

			int x = 0;
			while ( cursor < end )
			{
				int run = unchecked( (sbyte)encoded[cursor++] );
				int pixels = Math.Abs( run );
				if ( run == 0 || x + pixels > width || cursor + (run < 0 ? 1 : pixels) > end )
				{
					throw new InvalidDataException( "Invalid TPC pixel run." );
				}

				byte repeated = run < 0 ? encoded[cursor++] : (byte)0;
				for ( int i = 0; i < pixels; i++ )
				{
					byte index = run < 0 ? repeated : encoded[cursor++];
					Array.Copy( palette, index * ColorChannels, rgba, (y * width + x++) * ColorChannels, ColorChannels );
				}
			}

			if ( x != width )
			{
				throw new InvalidDataException( "TPC scanline width does not match its frame." );
			}
		}

		if ( cursor != encoded.Length )
		{
			throw new InvalidDataException( "Unexpected trailing TPC pixel data." );
		}

		return new TextureData( width, height, rgba );
	}
}
