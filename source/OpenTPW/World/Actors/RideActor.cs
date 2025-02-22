namespace OpenTPW;

public class RideActor : Actor
{
	public RideVM VM { get; private set; }

	public RideActor( string rideArchive )
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
