namespace VsDebugLogger;

using Framework;
using Framework.FileSystem;
using static Framework.Statics;

internal class ResilientInputStream
{
	private readonly FilePath file_path;
	private long offset = 0;
	private SysIo.FileStream? file_stream;

	public ResilientInputStream( FilePath file_path, bool skip_existing )
	{
		this.file_path = file_path;
		if( skip_existing )
			skip_existing_file_content();
	}

	private void skip_existing_file_content()
	{
		if( !try_open_file( file_path, ref file_stream, ref offset ) )
			return;
		if( !try_get_file_length( file_path, ref file_stream, ref offset, out long file_stream_length ) )
			return;
		Log.Info( $"Skipping {file_stream_length} bytes already in the file." );
		offset = file_stream_length;
	}

	public bool ReadNext( Function<bool, string> text_consumer )
	{
		if( !try_get_file_length( file_path, ref file_stream, ref offset, out long file_stream_length ) )
			return false;

		if( file_stream_length < offset )
		{
			Log.Info( "File has shrunk, starting from the beginning." );
			offset = 0;
		}

		long length = file_stream_length - offset;
		int count = length < int.MaxValue ? (int)length : int.MaxValue;
		(byte[]? buffer, int n) = try_read( file_path, ref file_stream, ref offset, count ) ?? default;
		if( buffer == null )
			return false;
		string text = SysText.Encoding.UTF8.GetString( buffer, 0, n );
		if( text.Length > 0 )
			if( !text_consumer.Invoke( text ) )
				return false;
		offset += n;
		return true;
	}

	private static bool try_open_file( FilePath file_path, ref SysIo.FileStream? file_stream, ref long offset )
	{
		if( file_stream == null )
		{
			try
			{
				file_stream = new SysIo.FileStream( file_path.FullName, SysIo.FileMode.Open, SysIo.FileAccess.Read, SysIo.FileShare.ReadWrite | SysIo.FileShare.Delete );
			}
			catch( Sys.Exception exception )
			{
				Log.Error( $"Failed to open file '{file_path}': {exception.GetType()}: {exception.Message}" );
				offset = 0;
				return false;
			}
		}
		return true;
	}

	private static bool try_get_file_length( FilePath file_path, ref SysIo.FileStream? file_stream, ref long offset, out long length )
	{
		if( True )
		{
			try
			{
				// PEARL: FileSystemInfo.Length will not return the updated file size unless FileSystemInfo.Refresh() is invoked first. It remains to be seen
				// whether these two operations are faster than just invoking FileStream.Length.
				file_path.Refresh();
				length = file_path.Length;
			}
			catch( Sys.Exception exception )
			{
				Log.Error( $"Failed to query length of file '{file_path}': {exception.GetType()}: {exception.Message}" );
				offset = 0;
				length = 0;
				return false;
			}
			return true;
		}
		else
		{
			if( !try_open_file( file_path, ref file_stream, ref offset ) )
			{
				length = 0;
				return false;
			}
			Assert( file_stream != null );
			try
			{
				length = file_stream.Length;
			}
			catch( Sys.Exception exception )
			{
				Log.Error( $"Failed to query length of file '{file_path}': {exception.GetType()}: {exception.Message}" );
				file_stream.Close();
				file_stream = null;
				offset = 0;
				length = 0;
				return false;
			}
			return true;
		}
	}

	private static bool try_seek( FilePath file_path, ref SysIo.FileStream? file_stream, ref long offset )
	{
		if( !try_open_file( file_path, ref file_stream, ref offset ) )
			return false;
		Assert( file_stream != null );
		try
		{
			file_stream.Seek( offset, SysIo.SeekOrigin.Begin );
		}
		catch( Sys.Exception exception )
		{
			Log.Error( $"Failed to seek in file '{file_path}': {exception.GetType()}: {exception.Message}" );
			file_stream.Close();
			file_stream = null;
			return false;
		}
		return true;
	}

	private static (byte[], int)? try_read( FilePath file_path, ref SysIo.FileStream? file_stream, ref long offset, int count )
	{
		if( !try_seek( file_path, ref file_stream, ref offset ) )
			return null;
		Assert( file_stream != null );
		byte[] buffer = new byte[count];
		try
		{
			int n = file_stream.Read( buffer, 0, buffer.Length );
			return (buffer, n);
		}
		catch( Sys.Exception exception )
		{
			Log.Error( $"Failed to read from file '{file_path}': {exception.GetType()}: {exception.Message}" );
			file_stream.Close();
			file_stream = null;
			return null;
		}
	}
}
