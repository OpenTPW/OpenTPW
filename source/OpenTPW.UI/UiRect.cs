namespace OpenTPW.UI;

/// <summary>
/// A rectangle in the retail UI's 2048x1536 virtual units, stored as the tables store it: left, top, right, bottom
/// (four signed 16-bit words). Containment is half-open, as in the retail rectangle hit test
/// (Ui_RectShape_Contains, US 0x659e83 / TPI 0x850fe3).
/// </summary>
public readonly record struct UiRect( short Left, short Top, short Right, short Bottom )
{
	public bool Contains( int x, int y ) => x >= Left && y >= Top && x < Right && y < Bottom;

	/// <summary>The integer centre the element constructor stores (Ui_Element_Construct, US 0x65bcad / TPI 0x849c8d).</summary>
	public (short X, short Y) Centre => ((short)(Left + ((Right - Left) >> 1)), (short)(Top + ((Bottom - Top) >> 1)));

	public override string ToString() => $"{Left},{Top},{Right},{Bottom}";
}
