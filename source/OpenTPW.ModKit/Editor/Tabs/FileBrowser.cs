using ImGuiNET;
using System.Reflection;

namespace OpenTPW.ModKit;

[EditorMenu( "Editor/File Browser" )]
internal sealed class FileBrowser : BaseTab
{
	private static int ThumbnailsToCache = 0;
	private static int ThumbnailsCachedTotal = 0;

	private Dictionary<string, Type> fileHandlers = new();

	private void CreateFileViewer( string file )
	{
		var extension = Path.GetExtension( file );

		if ( !fileHandlers.TryGetValue( extension, out var handlerType ) )
		{
			return;
		}

		CurrentFileViewer ??= (Activator.CreateInstance( handlerType, new[] { file } ) as IFileViewer)!;
	}

	private void DrawPreviewForFile( string file )
	{
		CreateFileViewer( file );
		CurrentFileViewer?.DrawPreview();
	}

	private void CollectFileHandlers()
	{
		foreach ( var type in Assembly.GetAssembly( typeof( FileBrowser ) )!.GetTypes() )
		{
			var attrib = type.GetCustomAttribute<HandlesExtensionAttribute>();
			if ( attrib == null )
				continue;

			fileHandlers.Add( attrib.Extension, type );
		}
	}

	internal class FileSystemObject
	{
		public string Name { get; set; }
		public FileSystemDirectory? Parent { get; set; }
		protected FileSystemObject( string name, FileSystemDirectory? parent )
		{
			Name = name;
			Parent = parent;
		}

		private static string GetFileType( string path )
		{
			// TODO: Move these into file handlers
			return Path.GetExtension( path ).ToLower() switch
			{
				".bf4" => "Font",
				".dat" => "Character Lookup Table",
				".str" => "Localized Strings",
				".lips" => "Lip Sync Data",
				".md2" => "M3D2 Model",
				".map" => "Sound Map",
				".mtr" => "Material",
				".rse" => "Ride Script (Compiled)",
				".rss" => "Ride Script (Source)",
				".sdt" => "Sound Data",
				".wct" => "Texture",
				".tga" => "Targa Image",

				".wad" => "Archive",
				".sam" => "Settings",

				".tpws" => "Save File",
				".ints" => "Initial Save File",
				".lays" => "Online Save File",

				".txt" => "Text File",

				".tpc" => "Color Palette",
				".esp" => "Sprite",

				_ => "Unknown File Type"
			};
		}

		public static FileSystemFile FromFile( string path, FileSystemDirectory parent )
		{
			var modified = FileSystem.GetModifiedTime( path );
			var size = FileSystem.GetSize( path );
			var type = GetFileType( path );
			var isArchive = FileSystem.IsArchive( path );

			if ( Path.GetExtension( path ) == ".wct" )
			{
				ThumbnailsToCache++;
				_ = Editor.Instance.CacheThumbnailAsync( path ).ContinueWith( x => ThumbnailsCachedTotal++ );
			}

			return new( path, parent, modified, size, type, isArchive );
		}

		public static FileSystemDirectory FromDirectory( string path, FileSystemDirectory? parent = null, int depth = 0 )
		{
			var node = new FileSystemDirectory( path, parent, new() );

			if ( depth >= 1 )
				return node;

			foreach ( var file in FileSystem.GetFiles( path ) )
			{
				node.Children.Add( FromFile( file, node ) );
			}

			foreach ( var directory in FileSystem.GetDirectories( path ) )
			{
				node.Children.Add( FromDirectory( directory, node, ++depth ) );
			}

			return node;
		}
	}

	internal class FileSystemDirectory : FileSystemObject
	{
		public List<FileSystemObject> Children { get; set; }
		public FileSystemDirectory( string name, FileSystemDirectory parent, List<FileSystemObject> children ) : base( name, parent ) { Children = children; }
	}

	internal class FileSystemFile : FileSystemObject
	{
		public DateTime FileModified { get; set; }
		public long FileSize { get; set; }
		public string FileType { get; set; }
		public bool IsArchive { get; set; }

		public FileSystemFile( string name, FileSystemDirectory parent, DateTime modified, long size, string type, bool isArchive ) : base( name, parent )
		{
			FileModified = modified;
			FileSize = size;
			FileType = type;
			IsArchive = isArchive;
		}
	}

	private string _basePath = null!;
	public string BasePath
	{
		get => _basePath;
		set
		{
			if ( _basePath == value )
				return;

			_basePath = ChangePath( _basePath, value );
			Populate();
		}
	}

	private string ChangePath( string oldPath, string newPath )
	{
		if ( newPath == ".." )
			return Directory.GetParent( oldPath )?.FullName ?? oldPath;

		if ( newPath == "." )
			return oldPath;

		return newPath;
	}

	public FileSystemDirectory BaseNode;
	public FileSystemDirectory CurrentDirectory;
	public FileSystemFile CurrentFile;

	public IFileViewer CurrentFileViewer;

	private void ClearFileViewer()
	{
		CurrentFileViewer?.Dispose();
		CurrentFileViewer = null;
	}

	public FileBrowser()
	{
		BasePath = "";

		CollectFileHandlers();
	}

	private void Populate()
	{
		ThumbnailsCachedTotal = 0;
		ThumbnailsToCache = 0;

		BaseNode = FileSystemObject.FromDirectory( BasePath );
		CurrentDirectory = BaseNode;
		CurrentFile = null;
	}

	private void DrawContextMenu()
	{
		var fileName = Path.GetFileName( CurrentFile.Name );

		ImGui.Text( $"{fileName} ({CurrentFile.FileType})" );
		ImGui.Separator();

		if ( ImGui.BeginMenu( "Open in..." ) )
		{
			if ( ImGui.MenuItem( "ImHex" ) )
				Utility.LaunchImHex( CurrentFile.Name );

			if ( ImGui.MenuItem( "Windows Explorer" ) )
				Utility.LaunchExplorer( CurrentFile.Name );

			if ( ImGui.MenuItem( "Dump" ))
				Utility.DumpFile( CurrentFile.Name );

			ImGui.EndMenu();
		}

		if ( ImGui.BeginMenu( "Copy..." ) )
		{
			ImGui.MenuItem( "Relative Path" );
			ImGui.MenuItem( "Absolute Path" );
			ImGui.MenuItem( "File Name" );
			ImGui.EndMenu();
		}
	}

	private void DrawFiles()
	{
		void DrawNode( FileSystemDirectory node )
		{
			var childFiles = node.Children.OfType<FileSystemFile>().ToList();
			var childDirectories = node.Children.OfType<FileSystemDirectory>().ToList();

			if ( node.Parent != null )
			{
				var directoryNavigators = new List<string>() { ".." };

				foreach ( var navigator in directoryNavigators )
				{
					var uniqueName = $"{navigator}##{navigator.GetHashCode()}_fileView";
					ImGui.TableNextRow();
					ImGui.TableNextColumn();

					if ( ImGui.Selectable( $"{uniqueName}" ) )
					{
						BasePath = node.Parent.Name;
					}
				}
			}

			foreach ( var directory in childDirectories )
			{
				var relativePath = FileSystem.GetRelativePath( directory.Name );
				var uniqueName = $"{relativePath}##{directory.GetHashCode()}_fileView";
				ImGui.TableNextRow();
				ImGui.TableNextColumn();

				if ( ImGui.Selectable( $"{uniqueName}" ) )
				{
					BasePath = directory.Name;
				}
			}

			foreach ( var file in childFiles )
			{
				var uniqueName = $"{FileSystem.GetRelativePath( file.Name )}##{file.GetHashCode()}_fileView";
				var selected = CurrentFile?.Name == file.Name;

				ImGui.TableNextRow();
				ImGui.TableNextColumn();

				if ( ImGui.Selectable( $"{uniqueName}", selected ) )
				{
					CurrentFile = file;
					ClearFileViewer();
				}

				if ( ImGui.BeginPopupContextItem() )
				{
					CurrentFile = file;
					ClearFileViewer();
					DrawContextMenu();
					ImGui.EndPopup();
				}

				ImGui.TableNextColumn();
				ImGui.Text( $"{file.FileType}" );
				ImGui.TableNextColumn();
				ImGui.Text( $"{file.FileModified}" );
				ImGui.TableNextColumn();
				ImGui.Text( $"{file.FileSize.ToSize()}" );
			}
		}

		DrawNode( CurrentDirectory );
	}

	public override void Draw()
	{
		var margin = 24f;

		if ( ImGui.Begin( "File Browser", ref visible ) )
		{
			if ( ImGui.Button( "^" ) )
				BasePath = "..";
			ImGui.SameLine();

			var unboxedBasePath = BasePath;
			ImGui.SetNextItemWidth( -1 );
			if ( ImGui.InputText( "##Directory", ref unboxedBasePath, 512, ImGuiInputTextFlags.EnterReturnsTrue ) )
				BasePath = unboxedBasePath;

			var previewWidth = (CurrentFile == null) ? 0 : 300;

			var windowWidth = ImGui.GetWindowWidth() - margin;
			var fileViewWidth = windowWidth - previewWidth;

			if ( ImGui.BeginTable( "##FileView", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY, new Vector2( fileViewWidth, -1 ) ) )
			{
				ImGui.TableSetupColumn( $"File", ImGuiTableColumnFlags.DefaultSort, 0.5f );
				ImGui.TableSetupColumn( $"Type", ImGuiTableColumnFlags.DefaultSort, 0.2f );
				ImGui.TableSetupColumn( $"Modified", ImGuiTableColumnFlags.DefaultSort, 0.2f );
				ImGui.TableSetupColumn( $"Size", ImGuiTableColumnFlags.DefaultSort, 0.1f );
				ImGui.TableHeadersRow();

				DrawFiles();
				
				ImGui.EndTable();
			}


			if ( CurrentFile != null )
			{
				ImGui.SameLine();

				if ( ImGui.BeginChild( "##Preview", new System.Numerics.Vector2( previewWidth, -1 ) ) )
				{
					var fileName = Path.GetFileName( CurrentFile.Name );

					DrawPreviewForFile( CurrentFile.Name );

					if ( ImGui.BeginTable( "##PreviewInfo", 2, ImGuiTableFlags.None, new Vector2( 325, 0 ) ) )
					{
						ImGui.TableSetupColumn( $"Name", ImGuiTableColumnFlags.DefaultHide, 0.33f );
						ImGui.TableSetupColumn( $"Value", ImGuiTableColumnFlags.DefaultSort, 0.66f );

						var rows = new Dictionary<string, string>()
						{
							{ "Name", fileName },
							{ "Modified", $"{CurrentFile.FileModified}" },
							{ "Type", $"{CurrentFile.FileType}" },
							{ "Size", $"{CurrentFile.FileSize.ToSize()}" },
							{ "Inside Archive?", $"{(CurrentFile.IsArchive ? "Yes" : "No")}" }
						};

						foreach ( var row in rows )
						{
							ImGui.TableNextColumn();
							ImGui.Text( row.Key );
							ImGui.TableNextColumn();
							ImGui.Text( row.Value );
						}
						
						ImGui.EndTable();
					}

					ImGui.EndChild();
				}
			}
		}

		if ( ThumbnailsToCache != ThumbnailsCachedTotal )
		{
			if ( ImGui.BeginPopup( "Caching Thumbnails..." ) )
			{
				ImGui.Text( $"{ThumbnailsCachedTotal} of {ThumbnailsToCache}" );

				ImGui.EndPopup();
			}
		}

		ImGui.End();
	}
}
