using Veldrid;

namespace OpenTPW;

public class Shader : Resource
{
	private ShaderInfo shaderInfo;

	public Veldrid.Shader[] ShaderProgram => shaderInfo.ShaderProgram;
	public bool IsDirty { get; private set; }
	public Action OnRecompile { get; set; }

	private FileSystemWatcher watcher;

	public ResourceLayoutDescription[] ResourceLayoutDescriptions { get; private set; } 
	public ResourceLayout[] ResourceLayouts { get; private set; }

	internal Shader( string path )
	{
		Path = path;
		All.Add( this );

		Recompile();

		var directoryName = System.IO.Path.GetDirectoryName( Path );
		var fileName = System.IO.Path.GetFileName( Path );

		watcher = new FileSystemWatcher( directoryName, fileName );

		watcher.NotifyFilter = NotifyFilters.Attributes
							 | NotifyFilters.CreationTime
							 | NotifyFilters.DirectoryName
							 | NotifyFilters.FileName
							 | NotifyFilters.LastAccess
							 | NotifyFilters.LastWrite
							 | NotifyFilters.Security
							 | NotifyFilters.Size;

		watcher.EnableRaisingEvents = true;
		watcher.Changed += OnWatcherChanged;
	}

	public static bool IsFileReady( string path )
	{
		try
		{
			using ( FileStream inputStream = File.OpenRead( path ) )
				return inputStream.Length > 0;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	private void OnWatcherChanged( object sender, FileSystemEventArgs e )
	{
		IsDirty = true;
	}

	public void Recompile()
	{
		if ( !IsFileReady( Path ) )
			return;

		shaderInfo = ShaderCompiler.CompileShader( Path );
		OnRecompile?.Invoke();
		IsDirty = false;

		ResourceLayoutDescriptions = shaderInfo.Reflection.ResourceLayouts;
		ResourceLayouts = shaderInfo.Reflection.ResourceLayouts.Select( x => Device.ResourceFactory.CreateResourceLayout( x ) ).ToArray();
	}
}
