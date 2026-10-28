using System.Numerics;

namespace OpenTPW;

/// <summary>The independent audio LCG and selection rules at TP.exe 0x6b6170/0x6b6780/0x6b68f0.</summary>
public sealed class SoundSelection
{
	private uint state;
	public SoundSelection( uint seed ) => state = seed;
	public uint Next()
	{
		state = unchecked(state * 0x19660d + 0x3c6ef35f);
		return BitOperations.RotateRight( state, 13 );
	}
	public static SoundCategoryMap.Definition? Definition( SoundCategoryMap.Sound sound, uint draw )
	{
		if ( sound.Definitions.Count == 0 ) return null;
		if ( sound.Definitions.Count == 1 ) return sound.Definitions[0];
		uint cumulative = 0;
		foreach ( var definition in sound.Definitions )
		{
			cumulative = unchecked(cumulative + definition.Weight);
			if ( cumulative >= draw ) return definition;
		}
		return sound.Definitions[0];
	}
	public static SoundCategoryMap.Sample? Sample( SoundCategoryMap.Definition definition, uint draw, int previousIndex = -1, bool avoidRepeat = false )
	{
		if ( definition.Samples.Count == 1 ) return definition.Samples[0];
		for ( int index = 0; index < definition.Samples.Count; ++index )
			if ( definition.Samples[index].Threshold >= draw )
				return definition.Samples[avoidRepeat && definition.Samples.Count > 2 && index == previousIndex ? (index + 1) % definition.Samples.Count : index];
		return null;
	}
	public SoundCategoryMap.Definition? ChooseDefinition( SoundCategoryMap.Sound sound ) =>
		Definition( sound, sound.Definitions.Count > 1 ? Next() >> 16 : 0 );
	public SoundCategoryMap.Sample? ChooseSample( SoundCategoryMap.Definition definition, int previousIndex = -1, bool avoidRepeat = false ) =>
		Sample( definition, definition.Samples.Count > 1 ? Next() >> 16 : 0, previousIndex, avoidRepeat );
	public int Range( int first, int second )
	{
		int min = Math.Min( first, second ), max = Math.Max( first, second );
		return min == max ? min : min + (int)(Next() % (uint)(max - min));
	}
}
