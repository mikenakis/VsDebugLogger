namespace Framework.FileSystem;

using System.Collections.Generic;
using SysThread = SysThread;
using Sys = Sys;
using SysIo = SysIo;
using SysText = SysText;
using static Statics;

public sealed class FilePath : FileSystemPath
{
	public static FilePath FromAbsolutePath( string fullPath )
	{
		Assert( SysIo.Path.IsPathRooted( fullPath ) );
		return new FilePath( fullPath );
	}

	public static FilePath FromRelativePath( string path )
	{
		Assert( !SysIo.Path.IsPathRooted( path ) );
		string fullPath = SysIo.Path.GetFullPath( path );
		return FromAbsolutePath( fullPath );
	}

	public static FilePath FromRelativeOrAbsolutePath( string path )
	{
		return SysIo.Path.IsPathRooted( path ) ? FromAbsolutePath( path ) : FromRelativePath( path );
	}

	public static FilePath FromRelativeOrAbsolutePath( string path, DirectoryPath basePathIfRelative )
	{
		return SysIo.Path.IsPathRooted( path ) ? FromAbsolutePath( path ) : Of( basePathIfRelative, path );
	}

	public static FilePath GetTempFileName()
	{
		// PEARL: System.IO.Path.GetTempFileName() returns a unique filename which already contains the extension '.tmp', and there is nothing we can do about that.
		//        We cannot replace the '.tmp' extension with our own nor append our own extension to it, because:
		//        - there would be no guarantees anymore that the filename is unique.
		//        - a zero-length file with the returned filename has already been created.
		// PEARL ON PEARL: The Win32::GetTempFileName() which is used internally to implement this function DOES support passing the
		//        desired extension as a parameter; however, System.IO.Path.GetTempFileName() passes the hard-coded extension ".tmp" to it.
		string tempFileName = SysIo.Path.GetTempFileName();
		return FromAbsolutePath( tempFileName );
	}

	public static FilePath Of( DirectoryPath directoryPath, string fileName )
	{
		//Assert( fileName.IndexOfAny( SysIo.Path.GetInvalidFileNameChars() ) == -1 );
		string path = SysIo.Path.Combine( directoryPath.Path, fileName );
		return new FilePath( path );
	}

	public SysIo.FileInfo FileInfo { get; }
	protected override SysIo.FileSystemInfo FileSystemInfo => FileInfo;
	public string? DirectoryName => FileInfo.DirectoryName;
	public bool IsReadOnly => FileInfo.IsReadOnly;
	public long Length => FileInfo.Length;

	public FilePath( string fullPath )
	{
		Assert( SysIo.Path.IsPathRooted( fullPath ) );
		//Assert( path == SysIo.Path.Combine( NotNull( SysIo.Path.GetDirectoryName( path ) ), SysIo.Path.GetFileName( path ) ) ); Does not work with UNC paths. The following line, however, does.
		Assert( SysIo.Path.GetFullPath( fullPath ) == fullPath );
		FileInfo = new SysIo.FileInfo( fullPath );
	}

	public DirectoryPath GetDirectory() => get_directory(); //new DirectoryPath( SysIo.Path.GetDirectoryName( FullName )! );
	public DirectoryPath Directory => get_directory(); //new DirectoryPath( NotNull( SysIo.Directory.GetParent( FullName )! ).FullName );

	private DirectoryPath get_directory()
	{
		// PEARL: DotNet offers two ways of obtaining the directory of a file; which one is better? No one knows.
		string directoryName = SysIo.Path.GetDirectoryName( FullName )!;
		Assert( directoryName == SysIo.Directory.GetParent( FullName )!.FullName );
		return new DirectoryPath( directoryName );
	}

	public FilePath WithReplacedExtension( string extension ) => FromAbsolutePath( SysIo.Path.ChangeExtension( FullName, extension ) );
	public string GetFileNameAndExtension() => NotNull( SysIo.Path.GetFileName( FullName ) );
	public string GetFileNameWithoutExtension() => NotNull( SysIo.Path.GetFileNameWithoutExtension( FullName ) );
	public bool StartsWith( DirectoryPath other ) => FullName.StartsWith( other.Path, Sys.StringComparison.OrdinalIgnoreCase );
	public bool EndsWith( string suffix ) => FullName.EndsWith( suffix, Sys.StringComparison.OrdinalIgnoreCase );
	[Sys.Obsolete] public override bool Equals( object? other ) => other is FilePath kin && Equals( kin );
	public bool Equals( FilePath other ) => FullName.Equals( other.FullName, Sys.StringComparison.OrdinalIgnoreCase );
	public override int GetHashCode() => FullName.GetHashCode();
	public override string ToString() => FullName;

	public string ReadAllText( SysText.Encoding? encoding = null )
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.FileNotFoundException( FullName );
		return SysIo.File.ReadAllText( FullName, encoding ?? SysText.Encoding.UTF8 );
	}

	public byte[] ReadAllBytes()
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.FileNotFoundException( FullName );
		return SysIo.File.ReadAllBytes( FullName );
	}

	public void WriteAllText( string text, SysText.Encoding? encoding = null )
	{
		if( !Directory.Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.DirectoryNotFoundException( GetDirectory().Path );
		SysIo.File.WriteAllText( FullName, text, encoding ?? SysText.Encoding.UTF8 );
	}

	public void MoveTo( FilePath newPathName ) //This is essentially 'Rename'
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.FileNotFoundException( FullName );
		SysIo.File.Move( FullName, newPathName.FullName );
	}

	public void CopyTo( FilePath other )
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.FileNotFoundException( FullName );
		SysIo.File.Copy( FullName, other.FullName, true );
	}

	public IEnumerable<string> ReadLines()
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.FileNotFoundException( FullName );
		return SysIo.File.ReadLines( FullName );
	}

	public void WriteAllBytes( byte[] bytes )
	{
		if( !Directory.Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.FileNotFoundException( FullName );
		SysIo.File.WriteAllBytes( FullName, bytes );
	}

	public void SetCreationTime( Sys.DateTime utc )
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.FileNotFoundException( FullName );
		SysIo.File.SetCreationTimeUtc( FullName, utc );
	}

	public void SetLastWriteTime( Sys.DateTime utc )
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.FileNotFoundException( FullName );
		SysIo.File.SetLastWriteTimeUtc( FullName, utc );
	}

	public void Truncate()
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.FileNotFoundException( FullName );
		SysIo.File.WriteAllText( FullName, "" );
	}

	private void delete()
	{
		if( !Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			throw new SysIo.FileNotFoundException( "File not found", FullName );
		try
		{
			SysIo.File.Delete( FullName );
		}
		catch( Sys.Exception exception )
		{
			throw map_exception( exception, "Delete" );
		}
	}

	public void Delete( int retryCount = 10 )
	{
		for( int retry = 0;; retry++ )
		{
			try
			{
				delete();
				return;
			}
			catch( SharingViolationException )
			{
				if( retry < retryCount )
				{
					Log.Info( $"Retry {retry + 1} of {retryCount} while {FullName} is in use..." );
					SysThread.Thread.Sleep( 100 );
					continue;
				}
				throw;
			}
		}
	}

	public void CreateParentDirectoryIfNotExists()
	{
		DirectoryPath parent = Directory;
		if( parent.Exists ) //avoids a huge timeout penalty if this is a network path and the network is inaccessible.
			return;
		parent.CreateDirectory();
	}

	public SysIo.Stream OpenBinary( SysIo.FileAccess access = SysIo.FileAccess.Read, SysIo.FileShare? share = null )
	{
		share ??= access switch
		{
			SysIo.FileAccess.Read => SysIo.FileShare.Read,
			SysIo.FileAccess.Write => SysIo.FileShare.None,
			SysIo.FileAccess.ReadWrite => SysIo.FileShare.None,
			_ => throw new Sys.ArgumentOutOfRangeException( nameof(access), access, null )
		};
		try
		{
			return new SysIo.FileStream( FullName, SysIo.FileMode.Open, access, share.Value );
		}
		catch( Sys.Exception exception )
		{
			throw map_exception( exception, nameof(OpenBinary) );
		}
	}

	public SysIo.Stream CreateBinary( SysIo.FileAccess access = SysIo.FileAccess.Write, SysIo.FileShare share = SysIo.FileShare.None )
	{
		Assert( access == SysIo.FileAccess.Write || access == SysIo.FileAccess.ReadWrite );
		try
		{
			return new SysIo.FileStream( FullName, SysIo.FileMode.Create, access, share );
		}
		catch( Sys.Exception exception )
		{
			throw map_exception( exception, nameof(CreateBinary) );
		}
	}

	public SysIo.TextReader OpenText( SysText.Encoding? encoding = null )
	{
		SysIo.Stream fileStream = OpenBinary();
		return new SysIo.StreamReader( fileStream, encoding ?? SysText.Encoding.UTF8 );
	}

	private Sys.Exception map_exception( Sys.Exception exception, string operationName )
	{
		switch( exception )
		{
			case Sys.UnauthorizedAccessException _: // The caller does not have the required permission.-or- The file is an executable file that is in use.-or- is a directory.-or- it is a read-only file.
			case SysIo.DirectoryNotFoundException _: // The specified path is invalid (for example, it is on an unmapped drive)
			case SysIo.DriveNotFoundException _:
			case SysIo.EndOfStreamException _:
			case SysIo.FileLoadException _:
			case SysIo.FileNotFoundException _:
			case SysIo.PathTooLongException _:
			case SysIo.InternalBufferOverflowException _:
			case SysIo.InvalidDataException _:
				break;
			case SysIo.IOException ioException:
				// See https://www.hresult.info -- for example, https://www.hresult.info/FACILITY_WIN32/0x80070020
				switch( unchecked((uint)ioException.HResult) )
				{
					// PEARL: both HResult 0x80000009 and HResult 0x80070005 map to ACCESS_DENIED.
					case 0x80000009: return new AccessDeniedException( ioException, this, operationName );
					case 0x80070005: //Facility 0x007 = WIN32, Code 0x0005 = ACCESS_DENIED
						return new AccessDeniedException( ioException, this, operationName );
					case 0x80070020: //Facility 0x007 = WIN32, Code 0x0020 = SHARING_VIOLATION
						return new SharingViolationException( ioException, this, operationName );
					case 0x80070079: //The semaphore timeout period has expired
						break;
					case 0x800700e8: //The pipe is broken
						break;
					case 0x800700E7: //"All pipe instances are busy"
						break;
					case 0x80131620: //"Error during managed I/O". Has been observed to have message "Pipe is broken" but without an inner exception.
						break;
				}
				break;
		}
		return new FilePathException( exception, this, operationName );
	}

	public SysIo.TextWriter CreateText( bool createDirectoryIfNotExist = false )
	{
		if( createDirectoryIfNotExist )
			Directory.CreateIfNotExist();
		return SysIo.File.CreateText( FullName );
	}

	public void RenameTo( FilePath targetFilePath )
	{
		SysIo.File.Move( FullName, targetFilePath.FullName );
	}
}
