namespace OpenTPW;

/// <summary>Single-line name selection and editing (0x660839, 0x660e1a, 0x661cae).</summary>
internal sealed class LobbyNameEdit
{
	private readonly LobbyPlayerFlow flow;
	public string Text => flow.Name;
	public int Caret { get; private set; }
	public int Anchor { get; private set; }
	public int SelectionStart => Math.Min( Caret, Anchor );
	public int SelectionLength => Math.Abs( Caret - Anchor );
	public LobbyNameEdit( LobbyPlayerFlow flow ) => this.flow = flow;
	public void Open( string prompt ) { flow.Name = prompt; Anchor = 0; Caret = Text.Length; }
	public void SelectAll() { Anchor = 0; Caret = Text.Length; }
	public void MoveTo( int position, bool extend = false )
	{
		Caret = Math.Clamp( position, 0, Text.Length );
		if ( !extend ) Anchor = Caret;
	}
	public void Move( int direction, bool extend = false )
	{
		if ( !extend && SelectionLength > 0 ) MoveTo( direction < 0 ? SelectionStart : SelectionStart + SelectionLength );
		else MoveTo( Caret + direction, extend );
	}
	public void Insert( string input )
	{
		string filtered = LobbyPlayerFlow.Filter( input );
		if ( filtered.Length == 0 ) return;
		int count = Math.Min( filtered.Length, LobbyPlayerFlow.MaxNameLength - (Text.Length - SelectionLength) );
		if ( count <= 0 ) return;
		int start = SelectionStart;
		flow.Name = Text[..start] + filtered[..count] + Text[(start + SelectionLength)..];
		MoveTo( start + count );
	}
	public void Delete( bool backwards )
	{
		if ( SelectionLength == 0 )
		{
			if ( backwards && Caret > 0 ) Anchor = Caret - 1;
			else if ( !backwards && Caret < Text.Length ) Anchor = Caret + 1;
		}
		int start = SelectionStart;
		flow.Name = Text.Remove( start, SelectionLength );
		MoveTo( start );
	}
}
