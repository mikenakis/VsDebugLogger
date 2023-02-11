namespace Framework.FileSystem;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Sys = Sys;
using SysIo = SysIo;
using static Statics;

public sealed class DirectoryPath : FileSystemPath
{
	public static DirectoryPath GetTempPath() => FromAbsolutePath( SysIo.Path.GetTempPath() );

	public static DirectoryPath FromAbsolutePath( string path ) => new DirectoryPath( path );

	public static DirectoryPath FromRelativePath( string path )
	{
		Assert( !SysIo.Path.IsPathRooted( path ) );
		string full_path = SysIo.Path.GetFullPath( path );
		return FromAbsolutePath( full_path );
	}

	public static DirectoryPath FromAbsoluteOrRelativePath( string path )
	{
		if( SysIo.Path.IsPathRooted( path ) )
			return FromAbsolutePath( path );
		return FromRelativePath( path == "" ? "." : path );
	}

	public static DirectoryPath NewTempPath()
	{
		//PEARL: DotNet offers a way to create a unique file, but there appears to be no way to create a unique directory. We attempt to remedy this here
		//by using the SysIo.Path.GetRandomFileName() function, which creates a random 8+3 filename, and then we use that filename as a directory name.
		//there is an imponderably small possibility that the filename already exists; we ignore it.
		DirectoryPath result = GetTempPath().SubDirectory( SysIo.Path.GetRandomFileName() );
		result.CreateIfNotExist();
		return result;
	}

	public string Path => FullName;
	public SysIo.DirectoryInfo DirectoryInfo { get; }
	protected override SysIo.FileSystemInfo FileSystemInfo => DirectoryInfo;

	public DirectoryPath( string full_path )
	{
		Assert( SysIo.Path.IsPathRooted( full_path ) );
		//Assert( full_path == SysIo.Path.Combine( NotNull( SysIo.Path.GetDirectoryName( full_path ) ), SysIo.Path.GetFileName( full_path ) ) ); Does not work with UNC paths. The following line, however, does.
		Assert( SysIo.Path.GetFullPath( full_path ) == full_path );
		DirectoryInfo = new SysIo.DirectoryInfo( strip_trailing_path_separator( full_path ) );
	}

	private static string strip_trailing_path_separator( string path )
	{
		if( new[] { SysIo.Path.DirectorySeparatorChar, SysIo.Path.AltDirectorySeparatorChar }.Contains( path[path.Length - 1] ) )
			return path.Substring( 0, path.Length - 1 );
		return path;
	}

	public string GetDirectoryName() => NotNull( SysIo.Path.GetFileName( Path ) );
	public DirectoryPath GetParent() => new DirectoryPath( NotNull( SysIo.Directory.GetParent( Path )! ).FullName );
	public bool StartsWith( DirectoryPath other ) => Path.StartsWith( other.Path, Sys.StringComparison.OrdinalIgnoreCase );
	public string GetRelativePath( DirectoryPath full_path ) => get_relative_path( Path, full_path.Path );
	public string GetRelativePath( FilePath full_path ) => get_relative_path( Path, full_path.FullName );
	[Sys.Obsolete] public override bool Equals( object? other ) => other is DirectoryPath kin && Equals( kin );
	public bool Equals( DirectoryPath other ) => Path.Equals( other.Path, Sys.StringComparison.OrdinalIgnoreCase );
	public override int GetHashCode() => Path.GetHashCode();
	public override string ToString() => Path;

	public DirectoryPath SubDirectory( string name )
	{
		Assert( name.IndexOfAny( SysIo.Path.GetInvalidFileNameChars() ) == -1 );
		return new DirectoryPath( SysIo.Path.Combine( Path, name ) );
	}

	public DirectoryPath TemporaryUniqueSubDirectory() => SubDirectory( SysIo.Path.GetRandomFileName() );

	public void CreateIfNotExist()
	{
		if( Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			return;
		// if( IsNetworkPath() ) I am not sure why I used to have this check here.
		// 	throw new Sys.UnauthorizedAccessException();
		SysIo.Directory.CreateDirectory( Path ); //PEARL: does not fail if the directory already exists!
	}

	public void MoveTo( DirectoryPath new_path_name )
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.DirectoryNotFoundException( Path );
		SysIo.Directory.Move( Path, new_path_name.Path );
	}

	public IEnumerable<SysIo.FileInfo> GetFileInfos( string pattern )
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.DirectoryNotFoundException( Path );
		return new SysIo.DirectoryInfo( Path ).GetFiles( pattern );
	}

	public void CreateDirectory()
	{
		// if( IsNetworkPath() )
		// 	throw new Sys.AccessViolationException( Path );
		SysIo.Directory.CreateDirectory( Path );
	}

	public IEnumerable<FilePath> EnumerateFiles( string pattern )
	{
		//PEARL: System.IO.Directory.GetFiles() will completely ignore the "path" parameter if the "pattern" parameter contains a path,
		//       and instead it will enumerate the files at the path contained within the pattern.
		//       To avoid this, we assert that the "pattern" parameter does not start with a path.
		Assert( !SysIo.Path.IsPathRooted( pattern ) );
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.DirectoryNotFoundException( Path );
		foreach( string s in SysIo.Directory.GetFiles( Path, pattern ) )
			yield return new FilePath( s );
	}

	public IEnumerable<DirectoryPath> EnumerateDirectories()
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.DirectoryNotFoundException( Path );
		foreach( string s in SysIo.Directory.GetDirectories( Path ) )
			yield return new DirectoryPath( s );
	}

	public override void Delete()
	{
		try
		{
			SysIo.Directory.Delete( Path, true );
		}
		catch( Sys.Exception exception )
		{
			throw new Sys.Exception( $"Could not delete directory {Path}", exception );
		}
	}

	public IEnumerable<FilePath> GetFiles( string pattern, bool recurse ) //
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.DirectoryNotFoundException( Path );
		return SysIo.Directory.GetFiles( Path, pattern, recurse ? SysIo.SearchOption.AllDirectories : SysIo.SearchOption.TopDirectoryOnly ).Select( FilePath.FromAbsolutePath );
	}

	private static string get_relative_path( string short_path, string long_path )
	{
		if( !long_path.StartsWith( short_path, Sys.StringComparison.Ordinal ) )
		{
			Assert( false );
			return long_path;
		}
		int start = short_path.Length;
		while( start < long_path.Length && "\\/".Contains( long_path[start] ) )
			start++;
		return long_path.Substring( start );
	}

	private IEnumerable<SysIo.FileInfo> get_matching_files( string pattern )
	{
		foreach( var entry in new SysIo.DirectoryInfo( Path ).EnumerateFileSystemInfos( pattern, SysIo.SearchOption.TopDirectoryOnly ) )
			if( entry is SysIo.FileInfo file_info )
				yield return file_info;
	}

	private IEnumerable<SysIo.DirectoryInfo> get_matching_directories( string pattern )
	{
		foreach( var entry in new SysIo.DirectoryInfo( Path ).EnumerateFileSystemInfos( pattern, SysIo.SearchOption.TopDirectoryOnly ) )
			if( entry is SysIo.DirectoryInfo directory_info )
				yield return directory_info;
	}

	public FilePath GetSingleMatchingFile( string pattern )
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.DirectoryNotFoundException( Path );
		IReadOnlyList<SysIo.FileInfo> matching_entries = get_matching_files( pattern ).ToImmutableList();
		if( matching_entries.Count == 0 )
			throw new Sys.Exception( $"Path not found: {Path}/{pattern}" );
		return new FilePath( matching_entries.Single().FullName );
	}

	public DirectoryPath GetSingleMatchingSubdirectory( string pattern )
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.DirectoryNotFoundException( Path );
		IReadOnlyList<SysIo.DirectoryInfo> matching_entries = get_matching_directories( pattern ).ToImmutableList();
		if( matching_entries.Count == 0 )
			throw new Sys.Exception( $"Path not found: {Path}/{pattern}" );
		return new DirectoryPath( matching_entries.Single().FullName );
	}

	public void CopyTo( DirectoryPath target, bool if_newer )
	{
		recursive_copy_to( new SysIo.DirectoryInfo( Path ), new SysIo.DirectoryInfo( target.Path ), if_newer );
	}

	private static void recursive_copy_to( SysIo.DirectoryInfo source_directory_info, SysIo.DirectoryInfo target_directory_info, bool if_newer )
	{
		foreach( SysIo.DirectoryInfo source_sub_directory_info in source_directory_info.GetDirectories() )
		{
			//PEARL: System.IO.DirectoryInfo.CreateSubdirectory() fails silently if the directory to be created already exists!
			//       By definition, silent failure is when the requested operation is not performed and no exception is thrown.
			//       By definition, the operation that we request a method to perform is what the name of the method says.
			//       The name of the method is "CreateSubdirectory", it is not "CreateSubdirectoryUnlessItAlreadyExists".
			SysIo.DirectoryInfo target_sub_directory_info = target_directory_info.CreateSubdirectory( source_sub_directory_info.Name );
			recursive_copy_to( source_sub_directory_info, target_sub_directory_info, if_newer );
		}
		foreach( SysIo.FileInfo source_file_info in source_directory_info.GetFiles() )
		{
			SysIo.FileInfo target_file_info = new SysIo.FileInfo( SysIo.Path.Combine( target_directory_info.FullName, source_file_info.Name ) );
			if( if_newer && target_file_info.Exists && source_file_info.LastWriteTimeUtc > target_file_info.LastWriteTimeUtc )
				continue;
			source_file_info.CopyTo( target_file_info.FullName, true );
		}
	}
}
