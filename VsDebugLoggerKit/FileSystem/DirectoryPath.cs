namespace VsDebugLoggerKit.FileSystem;

using Sys = System;
using SysIo = System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using static VsDebugLoggerKit.Statics;

public sealed class DirectoryPath : FileSystemPath
{
	public static DirectoryPath GetTempPath() => FromAbsolutePath( SysIo.Path.GetTempPath() );

	public static DirectoryPath FromAbsolutePath( string path ) => new( path );

	public static DirectoryPath FromRelativePath( string path )
	{
		Assert( !SysIo.Path.IsPathRooted( path ) );
		string fullPath = SysIo.Path.GetFullPath( path );
		return FromAbsolutePath( fullPath );
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

	public DirectoryPath( string fullPath )
	{
		Assert( SysIo.Path.IsPathRooted( fullPath ) );
		//Assert( full_path == SysIo.Path.Combine( NotNull( SysIo.Path.GetDirectoryName( full_path ) ), SysIo.Path.GetFileName( full_path ) ) ); Does not work with UNC paths. The following line, however, does.
		Assert( SysIo.Path.GetFullPath( fullPath ) == fullPath );
		DirectoryInfo = new SysIo.DirectoryInfo( strip_trailing_path_separator( fullPath ) );
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
	public string GetRelativePath( DirectoryPath fullPath ) => get_relative_path( Path, fullPath.Path );
	public string GetRelativePath( FilePath fullPath ) => get_relative_path( Path, fullPath.FullName );
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

	public void MoveTo( DirectoryPath newPathName )
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.DirectoryNotFoundException( Path );
		SysIo.Directory.Move( Path, newPathName.Path );
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

	private static string get_relative_path( string shortPath, string longPath )
	{
		if( !longPath.StartsWith( shortPath, Sys.StringComparison.Ordinal ) )
		{
			Assert( false );
			return longPath;
		}
		int start = shortPath.Length;
		while( start < longPath.Length && "\\/".Contains( longPath[start] ) )
			start++;
		return longPath.Substring( start );
	}

	private IEnumerable<SysIo.FileInfo> get_matching_files( string pattern )
	{
		foreach( var entry in new SysIo.DirectoryInfo( Path ).EnumerateFileSystemInfos( pattern, SysIo.SearchOption.TopDirectoryOnly ) )
			if( entry is SysIo.FileInfo fileInfo )
				yield return fileInfo;
	}

	private IEnumerable<SysIo.DirectoryInfo> get_matching_directories( string pattern )
	{
		foreach( var entry in new SysIo.DirectoryInfo( Path ).EnumerateFileSystemInfos( pattern, SysIo.SearchOption.TopDirectoryOnly ) )
			if( entry is SysIo.DirectoryInfo directoryInfo )
				yield return directoryInfo;
	}

	public FilePath GetSingleMatchingFile( string pattern )
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.DirectoryNotFoundException( Path );
		IReadOnlyList<SysIo.FileInfo> matchingEntries = get_matching_files( pattern ).ToImmutableList();
		if( matchingEntries.Count == 0 )
			throw new Sys.Exception( $"Path not found: {Path}/{pattern}" );
		return new FilePath( matchingEntries.Single().FullName );
	}

	public DirectoryPath GetSingleMatchingSubdirectory( string pattern )
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.DirectoryNotFoundException( Path );
		IReadOnlyList<SysIo.DirectoryInfo> matchingEntries = get_matching_directories( pattern ).ToImmutableList();
		if( matchingEntries.Count == 0 )
			throw new Sys.Exception( $"Path not found: {Path}/{pattern}" );
		return new DirectoryPath( matchingEntries.Single().FullName );
	}

	public void CopyTo( DirectoryPath target, bool ifNewer )
	{
		recursive_copy_to( new SysIo.DirectoryInfo( Path ), new SysIo.DirectoryInfo( target.Path ), ifNewer );
	}

	private static void recursive_copy_to( SysIo.DirectoryInfo sourceDirectoryInfo, SysIo.DirectoryInfo targetDirectoryInfo, bool ifNewer )
	{
		foreach( SysIo.DirectoryInfo sourceSubDirectoryInfo in sourceDirectoryInfo.GetDirectories() )
		{
			//PEARL: System.IO.DirectoryInfo.CreateSubdirectory() fails silently if the directory to be created already exists!
			//       By definition, silent failure is when the requested operation is not performed and no exception is thrown.
			//       By definition, the operation that we request a method to perform is what the name of the method says.
			//       The name of the method is "CreateSubdirectory", it is not "CreateSubdirectoryUnlessItAlreadyExists".
			SysIo.DirectoryInfo targetSubDirectoryInfo = targetDirectoryInfo.CreateSubdirectory( sourceSubDirectoryInfo.Name );
			recursive_copy_to( sourceSubDirectoryInfo, targetSubDirectoryInfo, ifNewer );
		}
		foreach( SysIo.FileInfo sourceFileInfo in sourceDirectoryInfo.GetFiles() )
		{
			var targetFileInfo = new SysIo.FileInfo( SysIo.Path.Combine( targetDirectoryInfo.FullName, sourceFileInfo.Name ) );
			if( ifNewer && targetFileInfo.Exists && sourceFileInfo.LastWriteTimeUtc > targetFileInfo.LastWriteTimeUtc )
				continue;
			sourceFileInfo.CopyTo( targetFileInfo.FullName, true );
		}
	}
}
