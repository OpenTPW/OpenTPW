namespace OpenTPW;

partial class Renderer
{
	private List<Action> _deletionQueue = [];

	public void ScheduleDelete( Action action )
	{
		_deletionQueue.Add( action );
	}

	private void ProcessDeletionQueue()
	{
		foreach ( var item in _deletionQueue.ToArray() )
		{
			item.Invoke();
		}

		_deletionQueue.Clear();
	}
}
