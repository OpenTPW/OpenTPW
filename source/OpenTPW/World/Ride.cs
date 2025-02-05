namespace OpenTPW;

public class Ride : Thing
{
	public RideVM VM { get; private set; }

	public Ride( string rideArchive )
	{
		var rideName = Path.GetFileNameWithoutExtension( rideArchive );

		VM = new RideVM( FileSystem.OpenRead( rideArchive + "\\" + rideName + ".rse" ) );
		var settingsFile = new SettingsFile( FileSystem.OpenRead( rideArchive + "\\" + rideName + ".sam" ) );

		Log.Trace( $"Loaded ride {settingsFile.Entries.First( x => x.Key == "Info.Name" ).Value}" );
	}

	protected override void OnUpdate()
	{
		VM.Update();
	}
}
