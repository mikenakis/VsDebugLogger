namespace Framework.FileSystem;

using System.Threading.Tasks;
using Sys = Sys;
using SysIo = SysIo;
using static Statics;

///<summary>Common base class for <see cref="FilePath"/> and <see cref="DirectoryPath"/>.</summary>
public abstract class FileSystemPath
{
	protected abstract SysIo.FileSystemInfo FileSystemInfo { get; }

	//[NewtonsoftJson.JsonConstructor]
	protected FileSystemPath()
	{ }

	public bool Exists => Exists0();
	public string Name => FileSystemInfo.Name;
	public string FullName => FileSystemInfo.FullName;
	public SysIo.FileAttributes Attributes => FileSystemInfo.Attributes;
	public Sys.DateTime CreationTime => FileSystemInfo.CreationTimeUtc;
	public Sys.DateTime LastAccessTime => FileSystemInfo.LastAccessTimeUtc;
	public Sys.DateTime LastWriteTime => FileSystemInfo.LastWriteTimeUtc;
	public string Extension => get_extension();
	public DirectoryPath Root => DirectoryPath.FromAbsolutePath( SysIo.Directory.GetDirectoryRoot( FullName ) );
	public virtual void Delete() => FileSystemInfo.Delete();
	public void Refresh() => FileSystemInfo.Refresh();

	public void DeleteIfExists()
	{
		// if( IsNetworkPath() )
		// 	throw new Sys.AccessViolationException( Path );
		if( Exists )
			Delete();
	}

	public DirectoryPath WithoutRelativePath( string relative_path )
	{
		Assert( FullName.EndsWith( relative_path, Sys.StringComparison.Ordinal ) );
		return DirectoryPath.FromAbsolutePath( FullName.Substring( 0, FullName.Length - relative_path.Length ) );
	}

	private string get_extension()
	{
		//PEARL: DotNet offers two entirely different ways of obtaining the file extension. Which one is better? No one knows.
		string result = FileSystemInfo.Extension;
		Assert( result == SysIo.Path.GetExtension( FullName ) );
		return result;
	}

	public bool IsNetworkPath()
	{
		string full_name = FullName;
		if( full_name.StartsWith( @"//", Sys.StringComparison.Ordinal ) || full_name.StartsWith( @"\\", Sys.StringComparison.Ordinal ) )
			return true; // is a UNC path
		string root_path = NotNull( SysIo.Path.GetPathRoot( full_name ) ); // get drive letter or \\host\share (will not return null because `path` is not null)
		SysIo.DriveInfo drive_info = new SysIo.DriveInfo( root_path ); // get info about the drive
		return drive_info.DriveType == SysIo.DriveType.Network; // return true if a network drive
	}

	// PEARL: if you attempt to access a non-existent network path, Windows will hit you with an insanely long timeout before it reports an error.
	//        I am not sure how long this timeout is, but it is certainly far longer than my patience.
	//        To remedy this, each time we access a file or directory we first invoke this method to check whether it exists.
	//        This method detects whether the path is a network path, and if so, it checks for its presence using a reasonably short timeout.
	//        See https://stackoverflow.com/a/52661569/773113
	protected bool Exists0()
	{
		if( IsNetworkPath() )
		{
			var task = new Task<bool>( () => FileSystemInfo.Exists );
			task.Start();
			Sys.TimeSpan timeout = Sys.TimeSpan.FromSeconds( 2 );
			if( !task.Wait( timeout ) )
			{
				Log.Error( $"Waiting for network path {FileSystemInfo} timed out after {timeout.TotalSeconds} seconds" );
				return false; //the operation timed out, so for all practical purposes, the file or directory does not exist.
			}
			return task.Result;
		}
		return FileSystemInfo.Exists;
	}
}
