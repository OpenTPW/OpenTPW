using System.Runtime.InteropServices;
using System.Text;

namespace OpenTPW;

/// <summary>Composes the lobby's generated sign materials from the background carried by SGN v101.</summary>
public static class LobbySigns
{
	private static readonly Dictionary<string, Texture?> cache = new( StringComparer.OrdinalIgnoreCase );
	private static bool reportedMissingText;

	/// <summary>
	/// Returns the generated texture for a model's <c>sign1</c>/<c>sign2</c> material, or null for other
	/// materials. Text needs Windows GDI/Uniscribe; elsewhere the plain SGN background is used.
	/// </summary>
	public static Texture? Resolve( string modelPath, string materialName, string title )
	{
		// Only the two generated halves belong to the SGN atlas. Other material names
		// must continue through normal texture resolution rather than borrowing sign pixels.
		if ( !materialName.Equals( "sign1", StringComparison.OrdinalIgnoreCase ) &&
			!materialName.Equals( "sign2", StringComparison.OrdinalIgnoreCase ) ) return null;
		var path = Path.ChangeExtension( modelPath, ".sgn" );
		string key = path + ":" + materialName;
		if ( cache.TryGetValue( key, out var found ) ) return found;
		using var input = FileSystem.OpenRead( path );
		if ( input == null ) throw new FileNotFoundException( $"Sign definition not found: {path}", path );
		using var buffer = new MemoryStream();
		input.CopyTo( buffer );
		var lines = LocalizedLines( modelPath );
		TextureData composed;
		if ( OperatingSystem.IsWindows() ) composed = Compose( buffer.ToArray(), lines.First, lines.Second );
		else
		{
			if ( !reportedMissingText ) Log.Warning( "Lobby sign text needs Windows GDI and Uniscribe; showing sign backgrounds only" );
			reportedMissingText = true;
			composed = DecodeBackground( buffer.ToArray() );
		}
		var image = Split( composed, materialName.Equals( "sign1", StringComparison.OrdinalIgnoreCase ) ? 0 : 1 );
		return cache[key] = new Texture( image.Data, image.Width, image.Height );
	}

	/// <summary>SGN keeps float quantization factors rather than the short WCT fields.</summary>
	public static TextureData DecodeBackground( byte[] data )
	{
		if ( data.Length < 4 || BitConverter.ToInt32( data ) != 101 )
			throw new InvalidDataException( "Unsupported SGN version" );
		// Embedded images follow font/layout records and two raw text-lighting patterns.
		// Validate the complete compressed image descriptor rather than assuming a fixed offset.
		for ( int offset = 40; offset + 28 <= data.Length; offset++ )
		{
			if ( data[offset] != 'B' || data[offset + 1] != 'I' || data[offset + 2] != 'L' || data[offset + 3] != 'Z' ) continue;
			int header = offset - 36;
			int width = BitConverter.ToInt32( data, header ), height = BitConverter.ToInt32( data, header + 4 );
			int colorSize = BitConverter.ToInt32( data, header + 28 ), alphaSize = BitConverter.ToInt32( data, header + 32 );
			if ( width < 1 || width > 4096 || height < 1 || height > 4096 ||
				BitConverter.ToInt32( data, header + 8 ) != 4 || colorSize < 28 || alphaSize < 0 ||
				(long)offset + colorSize + alphaSize > data.Length ) continue;
			using var wct = new MemoryStream();
			using ( var writer = new BinaryWriter( wct, Encoding.ASCII, true ) )
			{
				writer.Write( (byte)0x12 ); // Embedded BILZ backgrounds use packed row coefficients.
				writer.Write( (byte)(alphaSize > 0 ? 1 : 0) );
				writer.Write( (byte)32 ); writer.Write( (byte)1 );
				writer.Write( (short)width ); writer.Write( (short)height );
				for ( int channel = 0; channel < 4; channel++ )
				{
					float factor = BitConverter.ToSingle( data, header + 12 + channel * 4 );
					writer.Write( checked((short)MathF.Round( channel == 0 ? (factor - 1) * 2 : channel == 3 ? factor - 1 : (factor - 1) * 4 )) );
				}
				writer.Write( colorSize ); writer.Write( alphaSize ); writer.Write( 0 );
				writer.Write( data, offset, colorSize + alphaSize );
			}
			wct.Position = 0;
			return new TextureFile( wct ).Data;
		}
		throw new InvalidDataException( "SGN has no supported background image" );
	}

	/// <summary>
	/// One SGN text line: font face/file, character spacing in percent, top Y in the 512x512 drawing, the raw LOGFONT,
	/// and the bevel/lighting parameters used to shade the text.
	/// </summary>
	public sealed record SignLine( string Face, string FontPath, int Spacing, int Y, byte[] LogFont,
		int Bevel, float Depth, float Blur, float Ambient, float Diffuse, float Specular, float Power,
		float Azimuth, float Elevation, int CachedHeight, int CachedTop );

	/// <summary>Decoded SGN v101: draw order, shadow mode per line, styles, lighting patterns and the embedded background.</summary>
	public sealed record SignDefinition( int Order, int[] Modes, SignLine[] Lines, byte[][] Styles,
		TextureData[] Patterns, TextureData Background );

	/// <summary>The 101 header and two records are read in the order used by 0x5e6f40.</summary>
	public static SignDefinition Decode( byte[] data )
	{
		using var reader = new BinaryReader( new MemoryStream( data ), Encoding.ASCII );
		if ( reader.ReadInt32() != 101 ) throw new InvalidDataException( "Unsupported SGN version" );
		int order = reader.ReadInt32();
		bool background = reader.ReadByte() != 0;
		int[] modes = { reader.ReadInt32(), reader.ReadInt32() };
		var lines = new SignLine[2];
		for ( int i = 0; i < 2; i++ )
		{
			byte[] face = reader.ReadBytes( 64 );
			string font = Encoding.ASCII.GetString( reader.ReadBytes( 260 ) ).Split( '\0' )[0];
			int spacing = reader.ReadInt32(), y = reader.ReadInt32();
			byte[] logFont = reader.ReadBytes( 60 );
			// Charset 134 is GB2312, 136 is Big5; older assets use DEFAULT_CHARSET instead.
			Encoding.RegisterProvider( CodePagesEncodingProvider.Instance );
			int codePage = font.Equals( "simyou.ttf", StringComparison.OrdinalIgnoreCase ) ? 936 : 950;
			string name = Encoding.GetEncoding( codePage ).GetString( face ).Split( '\0' )[0];
			lines[i] = new( name, font, spacing, y, logFont, reader.ReadInt32(), reader.ReadSingle(),
				reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
				reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadInt32(), reader.ReadInt32() );
		}
		var styles = new byte[2][];
		for ( int i = 0; i < 2; i++ ) styles[i] = modes[i] == 0 ? new byte[20] : reader.ReadBytes( 20 );
		var patterns = new TextureData[2];
		for ( int i = 0; i < 2; i++ )
		{
			int width = reader.ReadInt32(), height = reader.ReadInt32(), channels = reader.ReadInt32();
			if ( width < 1 || width > 4096 || height < 1 || height > 4096 || channels != 4 )
				throw new InvalidDataException( "Unsupported SGN pattern" );
			byte[] argb = reader.ReadBytes( checked(width * height * 4) );
			if ( argb.Length != width * height * 4 ) throw new EndOfStreamException();
			var rgba = new byte[argb.Length];
			for ( int n = 0; n < argb.Length; n += 4 )
			{ rgba[n] = argb[n + 1]; rgba[n + 1] = argb[n + 2]; rgba[n + 2] = argb[n + 3]; rgba[n + 3] = argb[n]; }
			patterns[i] = new( width, height, rgba );
		}
		if ( !background ) throw new InvalidDataException( "SGN has no background" );
		return new( order, modes, lines, styles, patterns, DecodeBackground( data ) );
	}

	public static (string First, string Second) LocalizedLines( string modelPath )
	{
		int gateObjectId = Path.GetFileNameWithoutExtension( modelPath ).ToLowerInvariant() switch
		{
			"jun_isle" or "jun_gate" => 1601,
			"hal_isle" or "hal_gate" => 2601,
			"fan_isle" or "fan_gate" => 4601,
			"spa_isle" or "spa_gate" => 3601,
			_ => throw new InvalidDataException( $"Unknown lobby sign model {modelPath}" )
		};
		return LobbyObjectNames.Lines( gateObjectId );
	}

	/// <summary>Splits the visual top-first atlas into two top-first 128-square materials.</summary>
	public static TextureData Split( TextureData image, int half )
	{
		if ( half is < 0 or > 1 || image.Width != 256 || image.Height < 128 )
			throw new InvalidDataException( "Invalid lobby sign atlas" );
		var rgba = new byte[128 * 128 * 4];
		for ( int y = 0; y < 128; y++ )
			// Retail 0x5e7620 reverses rows for its native texture storage. Our MD2 shader
			// already converts bottom-up model V to top-first pixels; do not reverse twice.
			Array.Copy( image.Data, (y * image.Width + half * 128) * 4, rgba, y * 128 * 4, 128 * 4 );
		return new( 128, 128, rgba );
	}

	/// <summary>Returns RGBA pixels with row zero at the visual top, matching TextureFile.</summary>
	public static TextureData Compose( byte[] data, string first, string second )
	{
		var sign = Decode( data );
		var image = sign.Background;
		string[] texts = { first, second };
		var masks = new byte[2][];
		var styled = new byte[2][];
		for ( int i = 0; i < 2; i++ )
		{
			masks[i] = NativeFont.Render( sign.Lines[i], texts[i] );
			styled[i] = OffsetMask( masks[i], sign.Styles[i] );
		}
		// Both mode-2 lines share the first shadow colour and a maximum-coverage mask.
		if ( sign.Modes[0] == 2 && sign.Modes[1] == 2 )
		{
			var combined = new byte[masks[0].Length];
			for ( int i = 0; i < combined.Length; i++ ) combined[i] = Math.Max( styled[0][i], styled[1][i] );
			Blend( image, combined, sign.Styles[0] );
		}
		else for ( int i = 0; i < 2; i++ ) if ( sign.Modes[i] == 2 ) Blend( image, styled[i], sign.Styles[i] );
		// Mode-1 shadows follow the selected text order (0x5e7620).
		for ( int pass = 0; pass < 2; pass++ )
		{
			int i = sign.Order == 0 ? pass : 1 - pass;
			if ( sign.Modes[i] == 1 ) Blend( image, styled[i], sign.Styles[i] );
			// Unshadowed text is laid over its own shadow in the same order as the retail compositor.
			var line = sign.Lines[i];
			Shade( image, masks[i], Bevel( masks[i], line.Bevel, line.Blur ), line, sign.Patterns[i] );
		}
		return image;
	}

	private static byte[] OffsetMask( byte[] source, byte[] style )
	{
		int dx = BitConverter.ToInt32( style, 12 ), dy = BitConverter.ToInt32( style, 16 );
		var result = new byte[source.Length];
		for ( int y = 0; y < 256; y++ )
		for ( int x = 0; x < 256; x++ )
			if ( x - dx >= 0 && x - dx < 256 && y - dy >= 0 && y - dy < 256 )
				result[y * 256 + x] = source[(y - dy) * 256 + x - dx];
		int radius = BitConverter.ToInt32( style, 8 );
		if ( radius != 0 ) result = Extremum( result, radius );
		float sigma = BitConverter.ToSingle( style, 4 );
		return sigma > 0 ? Blur( result, sigma ) : result;
	}

	private static byte[] Extremum( byte[] input, int radius )
	{
		int r = Math.Abs( radius );
		for ( int axis = 0; axis < 2; axis++ )
		{
			var output = new byte[input.Length];
			for ( int y = 0; y < 256; y++ )
			for ( int x = 0; x < 256; x++ )
			{
				int c = axis == 0 ? x : y;
				if ( radius < 0 && (c < r || c > 255 - r) ) continue;
				int v = radius > 0 ? 0 : 255;
				for ( int n = Math.Max( 0, c - r ); n <= Math.Min( 255, c + r ); n++ )
				{
					int sample = input[axis == 0 ? y * 256 + n : n * 256 + x];
					v = radius > 0 ? Math.Max( v, sample ) : Math.Min( v, sample );
				}
				output[y * 256 + x] = (byte)v;
			}
			input = output;
		}
		return input;
	}

	private static byte[] Blur( byte[] input, float sigma )
	{
		int size = 3 - (int)(sigma * -2.5), centre = (size - 1) / 2;
		var kernel = new double[size];
		double total = 0;
		for ( int i = 0; i < size; i++ ) total += kernel[i] = Math.Exp( -(i - centre) * (i - centre) / (2 * sigma * sigma) );
		for ( int i = 0; i < size; i++ ) kernel[i] /= total;
		for ( int axis = 0; axis < 2; axis++ )
		{
			var result = new byte[input.Length];
			for ( int y = 0; y < 256; y++ )
			for ( int x = 0; x < 256; x++ )
			{
				double value = 0;
				for ( int n = 0; n < size; n++ )
				{
					int xx = axis == 0 ? x + n - centre : x, yy = axis == 0 ? y : y + n - centre;
					if ( xx >= 0 && xx < 256 && yy >= 0 && yy < 256 ) value += input[yy * 256 + xx] * kernel[n];
				}
				result[y * 256 + x] = (byte)Math.Clamp( (int)value, 0, 255 );
			}
			input = result;
		}
		return input;
	}

	private static byte[] Bevel( byte[] coverage, int passes, float sigma )
	{
		var current = coverage.Select( x => x == 0 ? (byte)0 : (byte)255 ).ToArray();
		var height = new byte[current.Length];
		for ( int n = 0; n < passes; n++ )
		{
			var next = Extremum( current, -1 );
			float t = 1 - (float)n / passes;
			byte value = unchecked((byte)(int)((3 * t * t - 2 * t * t * t) * 256));
			for ( int i = 0; i < next.Length; i++ ) if ( next[i] != current[i] ) height[i] = value;
			current = next;
		}
		for ( int i = 0; i < height.Length; i++ ) height[i] |= current[i];
		if ( sigma <= 0 ) return height;
		int size = 3 - (int)(sigma * -2.5), centre = (size - 1) / 2;
		var weights = new double[size * size];
		double total = 0;
		for ( int y = 0; y < size; y++ )
		for ( int x = 0; x < size; x++ ) total += weights[y * size + x] = Math.Exp( -((x - centre) * (x - centre) + (y - centre) * (y - centre)) / (2 * sigma * sigma) );
		var result = (byte[])height.Clone();
		for ( int y = 0; y < 256; y++ )
		for ( int x = 0; x < 256; x++ )
		{
			if ( coverage[y * 256 + x] == 0 ) continue;
			double value = 0;
			for ( int yy = 0; yy < size; yy++ )
			for ( int xx = 0; xx < size; xx++ ) value += height[Math.Clamp( y + yy - centre, 0, 255 ) * 256 + Math.Clamp( x + xx - centre, 0, 255 )] * weights[yy * size + xx] / total;
			result[y * 256 + x] = (byte)(int)value;
		}
		return result;
	}

	private static void Shade( TextureData image, byte[] coverage, byte[] height, SignLine line, TextureData pattern )
	{
		float az = -line.Azimuth * MathF.PI / 180, el = line.Elevation * MathF.PI / 180;
		var light = System.Numerics.Vector3.Normalize( new( MathF.Cos( el ) * MathF.Cos( az ), -MathF.Sin( az ), -MathF.Sin( el ) * MathF.Cos( az ) ) );
		var half = System.Numerics.Vector3.Normalize( (light + System.Numerics.Vector3.UnitY) * 0.5f );
		int top = Array.FindIndex( coverage, v => v != 0 ) / 256;
		int bottom = Array.FindLastIndex( coverage, v => v != 0 ) / 256 + 1;
		pattern = ResizePattern( pattern, Math.Max( 1, bottom - top ) );
		for ( int y = 0; y < image.Height; y++ )
		for ( int x = 0; x < image.Width; x++ )
		{
			int p = y * 256 + x, a = coverage[p];
			if ( a == 0 ) continue;
			float dx = (height[p + (x < 255 ? 1 : 0)] - height[p]) * line.Depth / 255;
			float dy = (height[p + (y < 255 ? 256 : 0)] - height[p]) * line.Depth / 255;
			var normal = System.Numerics.Vector3.Normalize( new( dx, 1, dy ) );
			float diffuse = Math.Max( 0, System.Numerics.Vector3.Dot( normal, light ) ) * line.Diffuse;
			float specular = MathF.Pow( Math.Max( 0, System.Numerics.Vector3.Dot( normal, half ) ), line.Power ) * line.Specular;
			int q = ((y - top) * pattern.Width + x % pattern.Width) * 4;
			for ( int c = 0; c < 3; c++ )
			{
				int v = (int)Math.Clamp( pattern.Data[q + c] * (line.Ambient + diffuse) + specular * 255, 0, 255 );
				image.Data[p * 4 + c] = (byte)(image.Data[p * 4 + c] + ((v - image.Data[p * 4 + c]) * a >> 8));
			}
			image.Data[p * 4 + 3] = Math.Max( image.Data[p * 4 + 3], (byte)a );
		}
	}

	private static TextureData ResizePattern( TextureData source, int height )
	{
		var rgba = new byte[source.Width * height * 4];
		int step = (source.Height * 65536 + 32768) / height;
		for ( int y = 0; y < height; y++ )
		{
			int first = y * step >> 16, last = (y + 1) * step >> 16;
			if ( first == last ) throw new InvalidDataException( "SGN pattern is smaller than its text coverage" );
			for ( int x = 0; x < source.Width; x++ )
			for ( int c = 0; c < 4; c++ )
			{
				int value = 0;
				for ( int row = first; row < last; row++ ) value += source.Data[(row * source.Width + x) * 4 + c];
				rgba[(y * source.Width + x) * 4 + c] = (byte)(value / (last - first));
			}
		}
		return new( source.Width, height, rgba );
	}

	private static void Blend( TextureData image, byte[] mask, byte[] style )
	{
		for ( int i = 0; i < image.Width * image.Height; i++ )
		{
			int a = mask[i] * style[3] >> 8;
			for ( int c = 0; c < 3; c++ )
			{
				int value = style[c] * style[3] >> 8;
				image.Data[i * 4 + c] = (byte)(image.Data[i * 4 + c] + ((value - image.Data[i * 4 + c]) * a >> 8));
			}
			image.Data[i * 4 + 3] = Math.Max( image.Data[i * 4 + 3], (byte)a );
		}
	}

	/// <summary>
	/// Retail sign text path 0x5e56f0: GDI font from the SGN LOGFONT, Uniscribe layout, white on black at twice the size in
	/// an 8-bit DIB, then 2x2 averaged to coverage. A line that does not fit is retried with a bisected width.
	/// </summary>
	internal static class NativeFont
	{
		[StructLayout( LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode )]
		private struct LogFont
		{
			public int Height, Width, Escapement, Orientation, Weight;
			public byte Italic, Underline, StrikeOut, CharSet, OutPrecision, ClipPrecision, Quality, PitchAndFamily;
			[MarshalAs( UnmanagedType.ByValTStr, SizeConst = 32 )] public string Face;
		}

		[DllImport( "gdi32.dll" )] private static extern IntPtr CreateCompatibleDC( IntPtr dc );
		[DllImport( "gdi32.dll" )] private static extern bool DeleteDC( IntPtr dc );
		[DllImport( "gdi32.dll" )] private static extern bool GdiFlush();
		[DllImport( "gdi32.dll" )] private static extern bool DeleteObject( IntPtr obj );
		[DllImport( "gdi32.dll" )] private static extern IntPtr SelectObject( IntPtr dc, IntPtr obj );
		[DllImport( "gdi32.dll" )] private static extern IntPtr CreateDIBSection( IntPtr dc, byte[] info, uint usage, out IntPtr pixels, IntPtr section, uint offset );
		[DllImport( "gdi32.dll", CharSet = CharSet.Unicode )] private static extern IntPtr CreateFontIndirectW( ref LogFont font );
		[DllImport( "gdi32.dll" )] private static extern IntPtr AddFontMemResourceEx( byte[] data, uint size, IntPtr reserved, out uint count );
		[DllImport( "gdi32.dll" )] private static extern bool RemoveFontMemResourceEx( IntPtr handle );
		[DllImport( "gdi32.dll", CharSet = CharSet.Unicode )] private static extern int GetTextFaceW( IntPtr dc, int count, StringBuilder face );
		[DllImport( "gdi32.dll" )] private static extern uint SetBkColor( IntPtr dc, uint color );
		[DllImport( "gdi32.dll" )] private static extern uint SetTextColor( IntPtr dc, uint color );
		[DllImport( "usp10.dll", CharSet = CharSet.Unicode )] private static extern int ScriptStringAnalyse( IntPtr dc, string text,
			int length, int glyphs, int charset, uint flags, int requestedWidth, IntPtr control, IntPtr state,
			int[]? advances, IntPtr tab, IntPtr classes, out IntPtr analysis );
		[DllImport( "usp10.dll" )] private static extern int ScriptStringGetLogicalWidths( IntPtr analysis, int[] advances );
		[DllImport( "usp10.dll" )] private static extern IntPtr ScriptString_pSize( IntPtr analysis );
		[DllImport( "usp10.dll" )] private static extern int ScriptStringFree( ref IntPtr analysis );
		[DllImport( "usp10.dll" )] private static extern int ScriptStringOut( IntPtr analysis, int x, int y, uint options,
			ref Rect rect, int minimum, int maximum, bool disabled );

		[StructLayout( LayoutKind.Sequential )]
		private struct Rect { public int Left, Top, Right, Bottom; }

		public static byte[] Render( SignLine line, string text )
		{
			if ( !OperatingSystem.IsWindows() ) throw new PlatformNotSupportedException( "SGN TrueType composition requires Windows GDI and Uniscribe" );
			var result = new byte[256 * 256];
			if ( text.Length == 0 ) return result;
			string fontPath = AnimationFiles.ResolveFile( $"fonts/{line.FontPath}", "Sign font" );
			byte[] fontData = FileSystem.ReadAllBytes( fontPath );
			var aliases = FontNames.Read( fontData );
			IntPtr registration = AddFontMemResourceEx( fontData, (uint)fontData.Length, IntPtr.Zero, out uint count );
			if ( registration == IntPtr.Zero || count == 0 ) throw new InvalidDataException( $"Could not load sign font {fontPath}" );
			IntPtr dc = IntPtr.Zero, bitmap = IntPtr.Zero, oldBitmap = IntPtr.Zero, font = IntPtr.Zero, oldFont = IntPtr.Zero, analysis = IntPtr.Zero;
			try
			{
				dc = CreateCompatibleDC( IntPtr.Zero );
				byte[] info = new byte[40 + 256 * 4];
				BitConverter.GetBytes( 40 ).CopyTo( info, 0 );
				BitConverter.GetBytes( 512 ).CopyTo( info, 4 );
				BitConverter.GetBytes( -512 ).CopyTo( info, 8 );
				info[12] = 1; info[14] = 8;
				BitConverter.GetBytes( 256 ).CopyTo( info, 32 );
				for ( int i = 0; i < 256; i++ ) info[40 + i * 4] = info[41 + i * 4] = info[42 + i * 4] = (byte)i;
				bitmap = CreateDIBSection( dc, info, 0, out IntPtr pixels, IntPtr.Zero, 0 );
				if ( dc == IntPtr.Zero || bitmap == IntPtr.Zero ) throw new InvalidOperationException( "Could not allocate sign bitmap" );
				var highResolution = new byte[512 * 512];
				Marshal.Copy( highResolution, 0, pixels, highResolution.Length );
				oldBitmap = SelectObject( dc, bitmap );
				var lf = new LogFont
				{
					Height = BitConverter.ToInt32( line.LogFont, 0 ), Width = BitConverter.ToInt32( line.LogFont, 4 ),
					Escapement = BitConverter.ToInt32( line.LogFont, 8 ), Orientation = BitConverter.ToInt32( line.LogFont, 12 ),
					Weight = BitConverter.ToInt32( line.LogFont, 16 ), Italic = line.LogFont[20], Underline = line.LogFont[21],
					StrikeOut = line.LogFont[22], CharSet = line.LogFont[23], OutPrecision = line.LogFont[24],
					ClipPrecision = line.LogFont[25], Quality = line.LogFont[26], PitchAndFamily = line.LogFont[27], Face = line.Face
				};
				int low = 1, high = lf.Width == 0 ? -lf.Height : lf.Width;
				for ( int attempt = 0; ; attempt++ )
				{
					font = CreateFontIndirectW( ref lf );
					if ( font == IntPtr.Zero ) throw new InvalidOperationException( $"Could not create sign font {line.Face}" );
					oldFont = SelectObject( dc, font );
					var selectedFace = new StringBuilder( 64 );
					GetTextFaceW( dc, selectedFace.Capacity, selectedFace );
					if ( !FaceMatches( selectedFace.ToString(), line.Face, aliases ) )
						throw new InvalidDataException( aliases.Count == 0
							? $"Sign font {line.Face} resolved to {selectedFace}"
							: $"Sign font {line.Face} resolved to {selectedFace}; {line.FontPath} declares {string.Join( ", ", aliases.SelectMany( names => names ).Distinct( StringComparer.OrdinalIgnoreCase ).OrderBy( name => name, StringComparer.Ordinal ) )}" );
					Check( ScriptStringAnalyse( dc, text, text.Length, 0, -1, 0x80, 0, IntPtr.Zero, IntPtr.Zero, null, IntPtr.Zero, IntPtr.Zero, out analysis ) );
					var advances = new int[text.Length];
					Check( ScriptStringGetLogicalWidths( analysis, advances ) );
					for ( int i = 0; i < advances.Length; i++ ) advances[i] = advances[i] * line.Spacing / 100;
					Check( ScriptStringFree( ref analysis ) );
					Check( ScriptStringAnalyse( dc, text, text.Length, 0, -1, 0x80, 0, IntPtr.Zero, IntPtr.Zero, advances, IntPtr.Zero, IntPtr.Zero, out analysis ) );
					int width = Marshal.ReadInt32( ScriptString_pSize( analysis ) );
					if ( width < 512 && (attempt == 0 || high - low < 3) )
					{
						SetBkColor( dc, 0 ); SetTextColor( dc, 0xffffff );
						var clip = new Rect { Right = 512, Bottom = 512 };
						Check( ScriptStringOut( analysis, 256 - width / 2, line.Y, 4, ref clip, 0, 0, false ) );
						if ( !GdiFlush() ) throw new InvalidOperationException( "Could not flush sign text drawing" );
						Marshal.Copy( pixels, highResolution, 0, highResolution.Length );
						for ( int y = 0; y < 256; y++ )
						for ( int x = 0; x < 256; x++ )
						{
							int p = y * 1024 + x * 2;
							result[y * 256 + x] = (byte)((highResolution[p] + highResolution[p + 1] + highResolution[p + 512] + highResolution[p + 513]) >> 2);
						}
						return result;
					}
					if ( attempt > 16 || high <= 1 ) throw new InvalidDataException( $"Sign text cannot fit: {text}" );
					Check( ScriptStringFree( ref analysis ) );
					SelectObject( dc, oldFont ); DeleteObject( font ); font = IntPtr.Zero;
					if ( attempt != 0 ) { if ( width < 512 ) low = lf.Width; else high = lf.Width; }
					lf.Width = low + (high - low) / 2;
				}
			}
			finally
			{
				if ( analysis != IntPtr.Zero ) ScriptStringFree( ref analysis );
				if ( font != IntPtr.Zero ) { SelectObject( dc, oldFont ); DeleteObject( font ); }
				if ( bitmap != IntPtr.Zero ) { SelectObject( dc, oldBitmap ); DeleteObject( bitmap ); }
				if ( dc != IntPtr.Zero ) DeleteDC( dc );
				RemoveFontMemResourceEx( registration );
			}
		}

		// GDI answers with the family name of the face it actually selected, which is often an
		// English or localized alias of the requested name (新細明體 comes back as PMingLiU).
		// Accept only aliases the shipped file itself declares, so a silent fallback to an
		// unrelated font still fails; files without a readable name table keep the exact match.
		internal static bool FaceMatches( string selected, string requested, IReadOnlyList<HashSet<string>> faces )
		{
			if ( faces.Count == 0 ) return selected.Equals( requested, StringComparison.OrdinalIgnoreCase );
			return faces.Any( names => names.Contains( requested ) && names.Contains( selected ) );
		}

		private static void Check( int result )
		{
			if ( result < 0 ) Marshal.ThrowExceptionForHR( result );
		}
	}

	/// <summary>Preserves each sfnt face's localized family names as a separate identity.</summary>
	internal static class FontNames
	{
		public static IReadOnlyList<HashSet<string>> Read( byte[] data )
		{
			var faces = new List<HashSet<string>>();
			if ( data.Length < 12 ) return faces;
			if ( Encoding.ASCII.GetString( data, 0, 4 ) == "ttcf" )
			{
				uint count = U32( data, 8 );
				Require( data, 12, (long)count * 4 );
				for ( int i = 0; i < count; ++i ) ReadFace( data, U32( data, 12 + i * 4 ), faces );
			}
			else ReadFace( data, 0, faces );
			return faces;
		}

		private static void Require( byte[] data, long offset, long length )
		{
			if ( offset < 0 || length < 0 || offset > data.LongLength || length > data.LongLength - offset )
				throw new InvalidDataException( "Truncated font name data" );
		}

		private static int U16( byte[] data, int offset ) => data[offset] << 8 | data[offset + 1];
		private static uint U32( byte[] data, int offset ) => (uint)data[offset] << 24 |
			(uint)data[offset + 1] << 16 | (uint)data[offset + 2] << 8 | data[offset + 3];

		private static void ReadFace( byte[] data, uint position, List<HashSet<string>> faces )
		{
			Require( data, position, 12 );
			int face = (int)position, count = U16( data, face + 4 );
			Require( data, (long)face + 12, count * 16L );
			for ( int i = 0; i < count; ++i )
			{
				int record = face + 12 + i * 16;
				if ( Encoding.ASCII.GetString( data, record, 4 ) != "name" ) continue;
				uint offset = U32( data, record + 8 ), length = U32( data, record + 12 );
				Require( data, offset, length );
				if ( length < 6 ) throw new InvalidDataException( "Truncated font name table" );
				var names = ReadNames( data, (int)offset, length );
				if ( names.Count != 0 ) faces.Add( names );
				return;
			}
		}

		private static HashSet<string> ReadNames( byte[] data, int offset, uint length )
		{
			var names = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
			int count = U16( data, offset + 2 ), storage = U16( data, offset + 4 );
			if ( 6L + count * 12L > length || storage < 6L + count * 12L || storage > length )
				throw new InvalidDataException( "Invalid font name records" );
			Encoding.RegisterProvider( CodePagesEncodingProvider.Instance );
			for ( int i = 0; i < count; ++i )
			{
				int record = offset + 6 + i * 12;
				int platform = U16( data, record ), encoding = U16( data, record + 2 ), id = U16( data, record + 6 );
				if ( id != 1 && id != 4 && id != 16 ) continue;
				int size = U16( data, record + 8 ), relative = U16( data, record + 10 );
				if ( (long)storage + relative + size > length ) throw new InvalidDataException( "Truncated font name string" );
				Encoding decoder;
				if ( platform == 1 && encoding == 0 ) decoder = Encoding.GetEncoding( 10000 );
				else if ( (platform == 0 && encoding <= 6) || (platform == 3 && (encoding == 0 || encoding == 1 || encoding == 10)) )
				{
					if ( (size & 1) != 0 ) throw new InvalidDataException( "Odd UTF-16 font name length" );
					decoder = Encoding.BigEndianUnicode;
				}
				else continue;
				string name = decoder.GetString( data, offset + storage + relative, size ).Trim( '\0', ' ' );
				if ( name.Length != 0 ) names.Add( name );
			}
			return names;
		}
	}
}
