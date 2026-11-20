namespace OpenTPW;

partial class Graphics2D
{
	private static Point2 BaseScreenSize = new Point2( 1280, 720 );

	public class Scope : IDisposable
	{
		Point2 _oldScreenSize;

		public Scope( Point2 newScreenSize )
		{
			_oldScreenSize = BaseScreenSize;
			BaseScreenSize = newScreenSize;
		}

		void IDisposable.Dispose()
		{
			BaseScreenSize = _oldScreenSize;
		}
	}
}
