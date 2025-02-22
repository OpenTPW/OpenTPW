namespace OpenTPW;

public partial class RideVM
{
	[Flags]
	public enum VMFlags
	{
		None,

		Sign = 1,
		Zero = 2,

		All = Sign | Zero
	}
}
