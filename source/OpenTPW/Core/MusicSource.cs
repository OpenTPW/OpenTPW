namespace OpenTPW;

internal sealed class MusicSource : IDisposable
{
	private readonly Dictionary<int, SdtSoundBank> banks = new();
	public SoundCategoryFile.Sample[] Samples { get; }

	public MusicSource( BaseFileSystem files, string root, string categoryPath, int categoryId )
	{
		var categories = SoundCategoryFile.ReadCategories( files.ReadAllBytes( categoryPath + "SFX.map" ) );
		var category = categories.Single( c => c.Id == categoryId );
		// Until the simulation supplies intensity changes, use its starting group.
		Samples = category.Groups.FirstOrDefault() ?? [];
		if ( Samples.Length == 0 )
			throw new InvalidDataException( "Music category has no samples." );
		var names = SoundCategoryFile.ReadBanks( files.ReadAllBytes( categoryPath + "BANK.map" ) );
		try
		{
			foreach ( int bank in Samples.Select( s => s.Bank ).Distinct() )
			{
				if ( bank > names.Length )
					throw new InvalidDataException( "Music category refers to a missing bank." );
				string path = $"{root}/{names[bank - 1]}HD.sdt";
				if ( !files.FileExists( path ) ) path = $"{root}/{names[bank - 1]}HW.sdt";
				banks.Add( bank, new SdtSoundBank( files.OpenRead( path ) ) );
			}
			if ( Samples.Any( s => s.Number > banks[s.Bank].Count ) )
				throw new InvalidDataException( "Music category refers to a missing sample." );
		}
		catch { Dispose(); throw; }
	}

	public MP2File Read( int index ) => banks[Samples[index].Bank].ReadSound( Samples[index].Number );

	public int ChooseSample( Random random )
	{
		int roll = random.Next( 65536 );
		for ( int i = 0; i < Samples.Length; i++ )
			if ( roll <= Samples[i].Threshold ) return i;
		return Samples.Length - 1;
	}

	public void Dispose()
	{
		foreach ( var bank in banks.Values ) bank.Dispose();
		banks.Clear();
	}
}
