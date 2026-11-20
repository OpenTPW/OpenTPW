namespace OpenTPW.UI;

/// <summary>
/// One element built from a UI table: the retail element base (Ui_Element_Construct, US 0x65bcad / TPI 0x849c8d)
/// keeps type, flags, identifier, rectangle and parent, links children in table order, and sets the hit shape to its
/// own rectangle. Field names follow what the interpreter stores; where the retail role of a value is not established
/// the name says which opcode set it.
/// </summary>
public sealed class UiElement
{
	/// <summary>Flag 0x01: the element takes the pointer (Ui_HitTest, US 0x65db25 / TPI 0x84bc3b).</summary>
	public const uint FlagInteractive = 0x1;
	/// <summary>Flag 0x02: disabled; set and cleared by Ui_SetEnabled (US 0x65da8d / TPI 0x84bba3).</summary>
	public const uint FlagDisabled = 0x2;
	/// <summary>Flag 0x10 on a button: toggles its selected bit on each click (button handler, US 0x668f9c / TPI 0x85b901).</summary>
	public const uint FlagToggle = 0x10;
	/// <summary>Flag 0x20 on a button: auto-repeat while held (US 0x668dd9 / TPI 0x85b5f7).</summary>
	public const uint FlagRepeat = 0x20;
	/// <summary>Any of the top four flag bits also makes an element hit-testable (Ui_HitTest).</summary>
	public const uint FlagHitMask = 0xF0000000;

	/// <summary>The class the retail factory creates (Ui_CreateElement, US 0x65dd7b / TPI 0x84be91): 1 plain, 2 button, others not modelled here.</summary>
	public int Type { get; }
	public uint Flags { get; set; }
	public uint Id { get; }
	public UiRect Rect { get; }
	public UiElement? Parent { get; }
	public List<UiElement> Children { get; } = [];
	public UiHitShape Shape { get; set; }
	/// <summary>Byte +4 of the retail element: hidden elements neither draw nor take the pointer.</summary>
	public bool Hidden { get; set; }
	/// <summary>Drawing layer: root 1, each level two more, at most 0x100 (Ui_Element_Construct).</summary>
	public int Layer { get; }

	/// <summary>Opcode 1: root-name hash of the element's model (see <see cref="UiModelName"/>).</summary>
	public uint? ModelHash { get; set; }
	/// <summary>Opcode 2: a second model hash; its retail role is not established (one record in the TPI frontend tables).</summary>
	public uint? SecondModelHash { get; set; }
	/// <summary>Opcode 3: an extra rectangle; its retail role (FUN_00850f0c in TPI) is not established.</summary>
	public UiRect? ExtraRect { get; set; }
	/// <summary>Opcode 10: a rectangle given to list-like parents (class kinds 4 and 7); role not established.</summary>
	public UiRect? ListRect { get; set; }
	/// <summary>Opcode 11: a point list (count, then x/y pairs).</summary>
	public IReadOnlyList<(short X, short Y)>? Points { get; set; }
	/// <summary>Opcode 13: one point.</summary>
	public (short X, short Y)? Point { get; set; }
	/// <summary>Opcode 17: value stored at element +0x90, -1 when absent. Pages fill help text indices here.</summary>
	public int Value17 { get; set; } = -1;
	/// <summary>Opcode 18: value stored at element +0x94, -1 when absent.</summary>
	public int Value18 { get; set; } = -1;

	public bool Enabled => (Flags & FlagDisabled) == 0;

	public UiElement( int type, uint flags, uint id, UiRect rect, UiElement? parent )
	{
		Type = type;
		Flags = flags;
		Id = id;
		Rect = rect;
		Parent = parent;
		Shape = new UiRectShape( rect );
		Layer = Math.Min( parent == null ? 1 : parent.Layer + 2, 0x100 );
	}

	/// <summary>Depth-first search by identifier, as Ui_FindElement (US 0x65d7d0 / TPI 0x84b8e6) does from a root.</summary>
	public UiElement? Find( uint id )
	{
		if ( Id == id ) return this;
		foreach ( var child in Children )
			if ( child.Find( id ) is { } found ) return found;
		return null;
	}

	/// <summary>
	/// The element under a point (Ui_HitTest, US 0x65db25 / TPI 0x84bc3b): disabled or hidden elements and those without
	/// a hit flag never answer; every child is tested and the last one in table order that answers wins; only when no
	/// child answers does the element test its own shape.
	/// </summary>
	public UiElement? HitTest( int x, int y )
	{
		if ( (Flags & FlagDisabled) != 0 || Hidden ) return null;
		if ( (Flags & FlagInteractive) == 0 && (Flags & FlagHitMask) == 0 ) return null;
		UiElement? hit = null;
		foreach ( var child in Children )
			if ( child.HitTest( x, y ) is { } found ) hit = found;
		return hit ?? (Shape.Contains( x, y ) ? this : null);
	}

	/// <summary>
	/// Enables or disables the element; returns whether the state changed (Ui_SetEnabled, US 0x65da8d / TPI 0x84bba3,
	/// which also redraws and calls the element's change callback).
	/// </summary>
	public bool SetEnabled( bool enabled )
	{
		bool disabled = (Flags & FlagDisabled) != 0;
		if ( enabled != disabled ) return false;
		Flags = enabled ? Flags & ~FlagDisabled : Flags | FlagDisabled;
		EnabledChanged?.Invoke( this );
		return true;
	}

	public event Action<UiElement>? EnabledChanged;

	public override string ToString() => $"element type {Type} id 0x{Id:x} {Rect}";
}
