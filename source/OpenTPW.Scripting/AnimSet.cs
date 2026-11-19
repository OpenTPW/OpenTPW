namespace OpenTPW;

/// <summary>
/// Twelve animation slots for one model, ordered by the shared retail slot letter table at
/// Model_AnimationSlotLetters (US 0x6fe6bc / TPI 0x93b20c) (<c>C D I L S M E U W B R O</c>).
/// </summary>
public sealed class AnimSet
{
	/// <summary>Default slot letters in TRIGANIM/LOOPANIM slot order (Model_AnimationSlotLetters, US 0x6fe6bc / TPI 0x93b20c).</summary>
	public const string DefaultSlotLetters = "CDILSMEUWBRO";

	private readonly List<ModelAnimation>[] slots;

	public string SlotLetters { get; }

	public bool IsEmpty => slots.All( slot => slot.Count == 0 );

	private AnimSet( string slotLetters )
	{
		ArgumentException.ThrowIfNullOrEmpty( slotLetters );
		SlotLetters = slotLetters;
		slots = Enumerable.Range( 0, slotLetters.Length ).Select( _ => new List<ModelAnimation>() ).ToArray();
	}

	public ModelAnimation? Find( int slot, int variant )
	{
		if ( (uint)slot >= (uint)slots.Length ) return null;
		var list = slots[slot];
		return (uint)variant < (uint)list.Count ? list[variant] : null;
	}

	/// <summary>
	/// Loads numbered (<c>&lt;stem&gt;&lt;letter&gt;&lt;v+1&gt;.MD2</c>, Model_AnimationPathFormat US 0x74d2b4 / TPI 0x9aef74)
	/// or unnumbered (<c>&lt;stem&gt;&lt;letter&gt;.MD2</c>, Model_UnnumberedAnimationFormat US 0x74d2a8 / TPI 0x9aef64) clips via
	/// <paramref name="openClip"/>, matching Model_FindNodeAndLoadAnimations (US 0x461f10 / TPI 0x4c2080).
	/// </summary>
	public static AnimSet Load( string stem, Func<string, byte[]?> openClip, string slotLetters = DefaultSlotLetters, Action<string>? onWarning = null )
	{
		ArgumentNullException.ThrowIfNull( stem );
		ArgumentNullException.ThrowIfNull( openClip );
		var set = new AnimSet( slotLetters );
		for ( int s = 0; s < slotLetters.Length; s++ )
		{
			string prefix = stem + slotLetters[s];
			bool TryAdd( string name )
			{
				if ( openClip( name ) is not { } bytes ) return false;
				try
				{
					using var stream = new MemoryStream( bytes, writable: false );
					var anim = new ModelAnimation( stream );
					if ( anim.Length <= 0 && anim.Tracks.Count == 0 ) return false;
					set.slots[s].Add( anim );
					return true;
				}
				catch ( Exception e ) when ( e is InvalidDataException or IOException or ArgumentException or IndexOutOfRangeException or OverflowException )
				{
					onWarning?.Invoke( $"{name}: {e.Message}" );
					return false;
				}
			}

			for ( int v = 1; TryAdd( $"{prefix}{v}.MD2" ); v++ ) { }
			if ( set.slots[s].Count == 0 ) TryAdd( $"{prefix}.MD2" );
		}
		return set;
	}
}
