namespace VsDebugLogger;

using Framework;
using Framework.FileSystem;
using static Framework.Statics;

internal class ResilientInputStream
{
	private readonly FilePath filePath;
	private long offset = 0;
	private SysIo.FileStream? fileStream;

	public ResilientInputStream( FilePath filePath, bool skipExisting )
	{
		this.filePath = filePath;
		if( skipExisting )
			skip_existing_file_content();
	}

	private void skip_existing_file_content()
	{
		if( !try_open_file( filePath, ref fileStream, ref offset ) )
			return;
		if( !try_get_file_length( filePath, ref fileStream, ref offset, out long fileStreamLength ) )
			return;
		Log.Info( $"Skipping {fileStreamLength} bytes already in the file." );
		offset = fileStreamLength;
	}

	public bool ReadNext( Function<bool, string> textConsumer )
	{
		if( !try_get_file_length( filePath, ref fileStream, ref offset, out long fileStreamLength ) )
			return false;

		if( fileStreamLength < offset )
		{
			Log.Info( "File has shrunk, starting from the beginning." );
			offset = 0;
		}

		long length = fileStreamLength - offset;
		int count = length < int.MaxValue ? (int)length : int.MaxValue;
		(byte[]? buffer, int n) = try_read( filePath, ref fileStream, ref offset, count ) ?? default;
		if( buffer == null )
			return false;
		string text = SysText.Encoding.UTF8.GetString( buffer, 0, n );
		if( text.Length > 0 )
			if( !textConsumer.Invoke( text ) )
				return false;
		offset += n;
		return true;
	}

	private static bool try_open_file( FilePath filePath, ref SysIo.FileStream? fileStream, ref long offset )
	{
		if( fileStream == null )
		{
			try
			{
				fileStream = new SysIo.FileStream( filePath.FullName, SysIo.FileMode.Open, SysIo.FileAccess.Read, SysIo.FileShare.ReadWrite | SysIo.FileShare.Delete );
			}
			catch( Sys.Exception exception )
			{
				Log.Error( $"Failed to open file '{filePath}': {exception.GetType()}: {exception.Message}" );
				offset = 0;
				return false;
			}
		}
		return true;
	}

	private static bool try_get_file_length( FilePath filePath, ref SysIo.FileStream? fileStream, ref long offset, out long length )
	{
		if( True )
		{
			try
			{
				// PEARL: FileSystemInfo.Length will not return the updated file size unless FileSystemInfo.Refresh() is invoked first. It remains to be seen
				// whether these two operations are faster than just invoking FileStream.Length.
				filePath.Refresh();
				length = filePath.Length;
			}
			catch( Sys.Exception exception )
			{
				Log.Error( $"Failed to query length of file '{filePath}': {exception.GetType()}: {exception.Message}" );
				offset = 0;
				length = 0;
				return false;
			}
			return true;
		}
		else
		{
			if( !try_open_file( filePath, ref fileStream, ref offset ) )
			{
				length = 0;
				return false;
			}
			Assert( fileStream != null );
			try
			{
				length = fileStream.Length;
			}
			catch( Sys.Exception exception )
			{
				Log.Error( $"Failed to query length of file '{filePath}': {exception.GetType()}: {exception.Message}" );
				fileStream.Close();
				fileStream = null;
				offset = 0;
				length = 0;
				return false;
			}
			return true;
		}
	}

	private static bool try_seek( FilePath filePath, ref SysIo.FileStream? fileStream, ref long offset )
	{
		if( !try_open_file( filePath, ref fileStream, ref offset ) )
			return false;
		Assert( fileStream != null );
		try
		{
			fileStream.Seek( offset, SysIo.SeekOrigin.Begin );
		}
		catch( Sys.Exception exception )
		{
			Log.Error( $"Failed to seek in file '{filePath}': {exception.GetType()}: {exception.Message}" );
			fileStream.Close();
			fileStream = null;
			return false;
		}
		return true;
	}

	private static (byte[], int)? try_read( FilePath filePath, ref SysIo.FileStream? fileStream, ref long offset, int count )
	{
		if( !try_seek( filePath, ref fileStream, ref offset ) )
			return null;
		Assert( fileStream != null );
		byte[] buffer = new byte[count];
		try
		{
			int n = fileStream.Read( buffer, 0, buffer.Length );
			return (buffer, n);
		}
		catch( Sys.Exception exception )
		{
			Log.Error( $"Failed to read from file '{filePath}': {exception.GetType()}: {exception.Message}" );
			fileStream.Close();
			fileStream = null;
			return null;
		}
	}
}
