using System.Globalization;

namespace OpenTPW;

/// <summary>Quality-to-output budget at 0x517ff0, capped by the scheduler at 0x6b1130.</summary>
public static class SoundChannelPolicy
{
	public const int MaximumChannels = 30;
	public static int Limit( SettingsFile settings, int quality, int availableChannels = MaximumChannels )
	{
		if ( quality < 0 || quality > 100 || availableChannels < 1 ) throw new ArgumentOutOfRangeException( nameof(quality) );
		int Value( string key ) => int.TryParse( settings[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value ) ? value :
			throw new InvalidDataException( $"Invalid sound setting {key}" );
		int low = Value( "ChannelInfo[0].CHANNELS" ), middle = Value( "ChannelInfo[1].CHANNELS" );
		int lowSwitch = Value( "ChannelInfo[0].SWITCH" ), middleSwitch = Value( "ChannelInfo[1].SWITCH" ), highSwitch = Value( "ChannelInfo[2].SWITCH" );
		if ( low < 1 || middle < low || lowSwitch < 0 || middleSwitch <= lowSwitch || highSwitch < middleSwitch || highSwitch >= 100 )
			throw new InvalidDataException( "Invalid sound channel quality intervals" );
		// Retail asks the backend for its maximum only above the third switch.
		int count = quality > highSwitch ? middle + (availableChannels - middle) * (quality - middleSwitch) / (100 - middleSwitch) :
			quality > middleSwitch ? middle : low + (quality - lowSwitch) * (middle - low) / (middleSwitch - lowSwitch);
		return Math.Clamp( count, 1, Math.Min( availableChannels, MaximumChannels ) );
	}
}
