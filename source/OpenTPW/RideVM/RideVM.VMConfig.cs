namespace OpenTPW;

public partial class RideVM
{
	public struct VMConfig
	{
		public int StackSize { get; set; }
		public int LimboSize { get; set; }
		public int BounceSize { get; set; }
		public int WalkSize { get; set; }
		public int TimeSlice { get; set; }
	}
}
