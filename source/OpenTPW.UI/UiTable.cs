namespace OpenTPW.UI;

/// <summary>One decoded table record, kept for tooling and tests; <see cref="Offset"/> is the opcode's word index.</summary>
public sealed record UiRecord( int Offset, int Depth, int Opcode, string Value, uint? ControlId, UiRect? Rect );

/// <summary>Which interpreter a table is read with: Theme Park Inc added opcode 0x13 (TPI 0x84deea); Theme Park World stops there.</summary>
public enum UiTableDialect
{
	ThemeParkWorld,
	ThemeParkInc,
}

/// <summary>
/// A retail UI table: a stream of 16-bit words, each record an opcode followed by its operands, interpreted by
/// Ui_InterpretTable (US 0x65fd58 / TPI 0x84deea). The interpreter runs on a current element: element opcodes create a
/// child of it and interpret the following records with the child as current until opcode 5; the other opcodes set a
/// property of the current element. Operand words are read with Ui_TableReadWord/Dword/Rect/Point
/// (US 0x65fc18… / TPI 0x84ddaa…): little-endian, rectangles as four signed words, 32-bit values as two words.
/// </summary>
public sealed class UiTable
{
	/// <summary>Elements created at the top level (retail tables have one root).</summary>
	public IReadOnlyList<UiElement> Roots { get; }
	public IReadOnlyList<UiRecord> Records { get; }
	/// <summary>Number of words consumed, including the terminating opcode 5.</summary>
	public int Length { get; }

	private UiTable( IReadOnlyList<UiElement> roots, IReadOnlyList<UiRecord> records, int length ) => (Roots, Records, Length) = (roots, records, length);

	/// <summary>The single root of a retail table.</summary>
	public UiElement Root => Roots.Count == 1 ? Roots[0] : throw new InvalidOperationException( $"The table has {Roots.Count} roots." );

	/// <summary>Reads a table from little-endian bytes (as stored in the executable).</summary>
	public static UiTable Read( ReadOnlySpan<byte> bytes, UiTableDialect dialect )
	{
		var words = new ushort[bytes.Length / 2];
		for ( int i = 0; i < words.Length; i++ ) words[i] = (ushort)(bytes[2 * i] | bytes[2 * i + 1] << 8);
		return Read( words, dialect );
	}

	/// <exception cref="InvalidDataException">The stream ends inside a record, or holds an opcode the dialect does not know.</exception>
	public static UiTable Read( ReadOnlySpan<ushort> words, UiTableDialect dialect )
	{
		var reader = new Reader( words.ToArray(), dialect );
		var top = new List<UiElement>();
		reader.Interpret( null, top, 0 );
		return new UiTable( top, reader.Records, reader.Position );
	}

	private sealed class Reader( ushort[] words, UiTableDialect dialect )
	{
		public int Position;
		public readonly List<UiRecord> Records = [];

		private ushort Word()
		{
			if ( Position >= words.Length ) throw new InvalidDataException( $"UI table ends inside a record at word {Position}" );
			return words[Position++];
		}

		private uint Dword() => Word() | (uint)Word() << 16;
		private UiRect Rect() => new( (short)Word(), (short)Word(), (short)Word(), (short)Word() );
		private (short, short) Point() => ((short)Word(), (short)Word());

		/// <summary>Interprets records with <paramref name="current"/> as the current element until opcode 5 (or an unknown opcode in the retail loop).</summary>
		public void Interpret( UiElement? current, List<UiElement> topLevel, int depth )
		{
			while ( true )
			{
				int start = Position;
				int opcode = Word();
				UiElement Create( int type, uint flags, uint id, UiRect rect, string value )
				{
					var element = new UiElement( type, flags, id, rect, current );
					(current?.Children ?? topLevel).Add( element );
					Records.Add( new UiRecord( start, depth, opcode, value, id, rect ) );
					return element;
				}
				void Note( string value, UiRect? rect = null ) => Records.Add( new UiRecord( start, depth, opcode, value, null, rect ) );
				UiElement Current() => current ?? throw new InvalidDataException( $"UI table opcode {opcode} at word {start} has no current element" );

				switch ( opcode )
				{
					case 0:
					{
						int type = (short)Word(); uint flags = Dword(); uint id = Dword(); var rect = Rect();
						Interpret( Create( type, flags, id, rect, $"type={type};flags=0x{flags:x}" ), topLevel, depth + 1 );
						break;
					}
					case 1: { uint hash = Dword(); Current().ModelHash = hash; Note( $"0x{hash:x8}" ); break; }
					case 2: { uint hash = Dword(); Current().SecondModelHash = hash; Note( $"0x{hash:x8}" ); break; }
					case 3: { var rect = Rect(); Current().ExtraRect = rect; Note( "", rect ); break; }
					case 4:
					{
						int kind = Word();
						UiHitShape shape = kind switch
						{
							1 => new UiRectShape( Rect() ),
							2 => CircleShape(),
							3 => EllipseShape(),
							4 => PolygonShape(),
							// The retail switch has no default: an unknown kind leaves the shape unchanged and reads nothing more.
							_ => Current().Shape,
						};
						Current().Shape = shape;
						Note( $"shape={kind}" );
						break;
					}
					case 5: Note( "" ); return;
					// 6/0xE and 7/0xF build the same elements; the two pairs differ only in their opcode number.
					case 6: case 0xE: { var r = Rect(); Interpret( Create( 2, 0x21, 1, r, "" ), topLevel, depth + 1 ); break; }
					case 7: case 0xF: { var r = Rect(); Interpret( Create( 2, 0x21, 2, r, "" ), topLevel, depth + 1 ); break; }
					// 8 also installs a fixed message handler (TPI 0x85e3c0) on the new button; its behaviour is not modelled.
					case 8: { var r = Rect(); Interpret( Create( 2, 0x1, 3, r, "" ), topLevel, depth + 1 ); break; }
					case 9:
					{
						// Only list-like parents (class kinds 4 and 7) without a child 1 read this record; otherwise the retail
						// interpreter reads no operands, so the same is done here to stay word-aligned with it.
						var parent = Current();
						if ( parent.Type is 4 or 7 && parent.Children.All( c => c.Id != 1 ) )
						{
							var r = Rect();
							Interpret( Create( 3, 0x10, 1, r, "" ), topLevel, depth + 1 );
						}
						else Note( "skipped" );
						break;
					}
					case 10:
					{
						var parent = Current();
						if ( parent.Type is 4 or 7 ) { var r = Rect(); parent.ListRect = r; Note( "", r ); }
						else Note( "skipped" );
						break;
					}
					case 0xB:
					{
						int count = Word();
						var points = new (short, short)[count];
						for ( int i = 0; i < count; i++ ) points[i] = Point();
						Current().Points = points;
						Note( string.Join( ";", points.Select( p => $"{p.Item1},{p.Item2}" ) ) );
						break;
					}
					case 0xC:
					{
						int column = (short)Word(); var r = Rect();
						Interpret( Create( 2, 0x1, (uint)(column + 0x10), r, $"column={column}" ), topLevel, depth + 1 );
						break;
					}
					case 0xD: { var p = Point(); Current().Point = p; Note( $"{p.Item1},{p.Item2}" ); break; }
					case 0x10:
					{
						int index = (short)Word(); var r = Rect();
						Interpret( Create( 6, 0x1, (uint)index | 0x11100000, r, $"index={index}" ), topLevel, depth + 1 );
						break;
					}
					case 0x11: { uint v = Dword(); Current().Value17 = (int)v; Note( $"0x{v:x8}" ); break; }
					case 0x12: { uint v = Dword(); Current().Value18 = (int)v; Note( $"0x{v:x8}" ); break; }
					case 0x13 when dialect == UiTableDialect.ThemeParkInc:
					{
						// Theme Park Inc only: an element of class 5 that is then registered (TPI 0x85d1a8); no child records follow.
						var r = Rect();
						Create( 5, 0x101, 3, r, "" );
						break;
					}
					default:
						// The retail loop simply stops on an unknown opcode; a table that relies on that is not one we can verify.
						throw new InvalidDataException( $"Unknown UI table opcode {opcode} at word {start}" );
				}
			}
		}

		private UiCircleShape CircleShape() { var (x, y) = Point(); return new UiCircleShape( x, y, (short)Word() ); }
		private UiEllipseShape EllipseShape() { var r = Rect(); return new UiEllipseShape( r.Left, r.Top, r.Right, r.Bottom ); }

		private UiPolygonShape PolygonShape()
		{
			int count = (short)Word();
			var points = new (short, short)[Math.Max( count, 0 )];
			for ( int i = 0; i < points.Length; i++ ) points[i] = Point();
			return new UiPolygonShape( points );
		}
	}
}
