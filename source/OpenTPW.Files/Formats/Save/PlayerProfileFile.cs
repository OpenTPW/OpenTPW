using System.Text;

namespace OpenTPW;

/// <summary>Version 12 gms.dat (0x5ac370, 0x5ad180, 0x423980). Unknown fields are retained byte for byte.</summary>
public sealed class PlayerProfileFile
{
	/// <summary>The four enabled/volume pairs in the US 0x423e90 preference block.</summary>
	public enum SoundChannel { Effects, Music, Speech, Movie }
	/// <summary>The seven trailing preference bytes serialized by US 0x423e90.</summary>
	public enum Preference { Advisor, Tutorial, Tooltips, ConfirmDelete, RightMouseScroll, RightMouseCancel, Isometric }
	public sealed record Segment( string Name, byte[] Data );
	public byte[] Prefix { get; }
	public IReadOnlyList<Segment> Segments { get; }
	public byte[] Preferences { get; }
	public ushort[] RideIds { get; }
	public byte CreationMode => Prefix[14];
	private static int SoundOffset( SoundChannel channel ) => channel is >= SoundChannel.Effects and <= SoundChannel.Movie
		? (int)channel * 8 : throw new ArgumentOutOfRangeException( nameof(channel) );
	private static int PreferenceOffset( Preference preference ) => preference is >= Preference.Advisor and <= Preference.Isometric
		? 32 + (int)preference : throw new ArgumentOutOfRangeException( nameof(preference) );
	public bool SoundEnabled( SoundChannel channel ) => Preferences[SoundOffset( channel )] != 0;
	public void SetSoundEnabled( SoundChannel channel, bool enabled )
	{
		// Native reads only the low byte of the four-byte enabled field. Preserve its padding.
		Preferences[SoundOffset( channel )] = enabled ? (byte)1 : (byte)0;
	}
	public int SoundVolume( SoundChannel channel )
	{
		uint value = BitConverter.ToUInt32( Preferences, SoundOffset( channel ) + 4 );
		return value <= 100 ? (int)value : throw new InvalidDataException( "Player volume must be between 0 and 100" );
	}
	public void SetSoundVolume( SoundChannel channel, int volume )
	{
		if ( volume is < 0 or > 100 ) throw new ArgumentOutOfRangeException( nameof(volume) );
		BitConverter.TryWriteBytes( Preferences.AsSpan( SoundOffset( channel ) + 4, 4 ), (uint)volume );
	}
	public bool Enabled( Preference preference ) => Preferences[PreferenceOffset( preference )] != 0;
	public void SetEnabled( Preference preference, bool enabled ) => Preferences[PreferenceOffset( preference )] = enabled ? (byte)1 : (byte)0;
	private PlayerProfileFile( byte[] prefix, Segment[] segments, byte[] preferences, ushort[] rides )
	{
		Prefix = prefix;
		Segments = Array.AsReadOnly( segments );
		Preferences = preferences;
		RideIds = rides;
	}

	public static PlayerProfileFile Read( byte[] data )
	{
		using var stream = new MemoryStream( data, false );
		using var reader = new BinaryReader( stream, Encoding.Latin1 );
		byte[] Take( int count )
		{
			if ( count < 0 || count > stream.Length - stream.Position ) throw new InvalidDataException( "Truncated player profile" );
			return reader.ReadBytes( count );
		}
		uint U32() => BitConverter.ToUInt32( Take( 4 ) );
		if ( U32() != 12 ) throw new InvalidDataException( "Unsupported player profile version" );
		byte[] prefix = Take( 17 );
		uint count = U32();
		if ( (long)count * 164 > stream.Length - stream.Position ) throw new InvalidDataException( "Invalid player segment count" );
		var segments = new Segment[count];
		for ( int i = 0; i < segments.Length; i++ )
		{
			uint length = U32();
			if ( length > int.MaxValue ) throw new InvalidDataException( "Invalid player segment name length" );
			string name = Encoding.Latin1.GetString( Take( (int)length ) );
			segments[i] = new( name, Take( 160 ) );
		}
		byte[] preferences = Take( 39 );
		uint rideCount = U32();
		if ( (long)rideCount * 2 != stream.Length - stream.Position ) throw new InvalidDataException( "Invalid player ride list or trailing data" );
		var rides = new ushort[rideCount];
		for ( int i = 0; i < rides.Length; i++ ) rides[i] = BitConverter.ToUInt16( Take( 2 ) );
		return new( prefix, segments, preferences, rides );
	}

	public byte[] Write()
	{
		if ( Prefix.Length != 17 || Preferences.Length != 39 || Segments.Any( segment => segment.Data.Length != 160 ) )
			throw new InvalidDataException( "Invalid player profile field length" );
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream, Encoding.Latin1, true );
		writer.Write( 12u );
		writer.Write( Prefix );
		writer.Write( (uint)Segments.Count );
		foreach ( var segment in Segments )
		{
			if ( segment.Name.Any( character => character > 255 ) ) throw new InvalidDataException( "Profile segment name is not a byte string" );
			byte[] name = Encoding.Latin1.GetBytes( segment.Name );
			writer.Write( (uint)name.Length );
			writer.Write( name );
			writer.Write( segment.Data );
		}
		writer.Write( Preferences );
		writer.Write( (uint)RideIds.Length );
		foreach ( ushort id in RideIds ) writer.Write( id );
		return stream.ToArray();
	}

	/// <summary>0x5ab370 creates empty lists and fixed prefix defaults; 0x5ac070 sets the creation mode.</summary>
	public static PlayerProfileFile Create( byte creationMode, byte[] preferences )
	{
		if ( creationMode > 1 || preferences.Length != 39 ) throw new ArgumentException( "Invalid player defaults" );
		byte[] prefix = new byte[17];
		prefix[14] = creationMode;
		prefix[15] = prefix[16] = 1;
		return new( prefix, [], (byte[])preferences.Clone(), [] );
	}

	/// <summary>0x423170 initializes the four enabled volume pairs from sound.sam, then seven preference bytes.</summary>
	public static byte[] DefaultPreferences( SettingsFile sound )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		if ( !int.TryParse( sound["SoundInfo.SOUNDON"], out int enabled ) ) throw new InvalidDataException( "Missing sound enable default" );
		foreach ( string channel in new[] { "SFX", "MUSIC", "SPEECH", "MOVIE" } )
		{
			if ( !uint.TryParse( sound[$"DefaultVolume.{channel}"], out uint volume ) || volume > 100 )
				throw new InvalidDataException( $"Invalid default volume for {channel}" );
			// Only the first byte of each four-byte enabled field has defined native meaning.
			writer.Write( enabled == 0 ? 0u : 1u );
			writer.Write( volume );
		}
		writer.Write( new byte[] { 1, 1, 1, 1, 0, 1, 1 } );
		return stream.ToArray();
	}
}
