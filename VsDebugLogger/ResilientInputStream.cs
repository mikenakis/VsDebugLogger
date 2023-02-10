namespace VsDebugLogger;

using VsDebugLogger.Framework;
using VsDebugLogger.Framework.FileSystem;
using Sys = System;
using SysText = System.Text;
using SysIo = System.IO;
using static Statics;

internal class ResilientInputStream
{
	private readonly Procedure<string> logger;
	private readonly FilePath file_path;
	private long offset = 0;
	private SysIo.FileStream? file_stream;

	public ResilientInputStream( FilePath file_path, Procedure<string> logger )
	{
		this.logger = logger;

		this.file_path = file_path;

		skip_existing_file_content();
	}

	private void skip_existing_file_content()
	{
		if( !try_open_file( file_path, ref file_stream, ref offset, logger ) )
			return;
		if( !try_get_file_length( file_path, ref file_stream, ref offset, out long file_stream_length, logger ) )
			return;
		logger.Invoke( $"Skipping '{file_stream_length}' bytes already in the file." );
		offset = file_stream_length;
	}

	public bool ReadNext( out string text )
	{
		text = null!;
		if( !try_get_file_length( file_path, ref file_stream, ref offset, out long file_stream_length, logger ) )
			return false;

		if( file_stream_length < offset )
		{
			logger.Invoke( "File has shrunk, starting from the beginning." );
			offset = 0;
		}

		SysText.StringBuilder string_builder = new SysText.StringBuilder();
		while( offset < file_stream_length )
		{
			long length = file_stream_length - offset;
			int count = length < int.MaxValue ? (int)length : int.MaxValue;
			(byte[]? buffer, int n) = try_read( file_path, ref file_stream, ref offset, count, logger ) ?? default;
			if( buffer == null )
				return false;
			if( n == 0 )
				break;
			offset += n;
			string_builder.Append( SysText.Encoding.UTF8.GetString( buffer, 0, n ) );
		}

		text = string_builder.ToString();
		return true;
	}

	private static bool try_open_file( FilePath file_path, ref SysIo.FileStream? file_stream, ref long offset, Procedure<string> logger )
	{
		if( file_stream == null )
		{
			try
			{
				file_stream = new SysIo.FileStream( file_path.FullName, SysIo.FileMode.Open, SysIo.FileAccess.Read, SysIo.FileShare.ReadWrite | SysIo.FileShare.Delete );
			}
			catch( Sys.Exception exception )
			{
				logger.Invoke( $"Failed to open file '{file_path}': {exception.GetType()}: {exception.Message}" );
				offset = 0;
				return false;
			}
		}
		return true;
	}

	private static bool try_get_file_length( FilePath file_path, ref SysIo.FileStream? file_stream, ref long offset, out long length, Procedure<string> logger )
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
				logger.Invoke( $"Failed to query length of file '{file_path}': {exception.GetType()}: {exception.Message}" );
				offset = 0;
				length = 0;
				return false;
			}
			return true;
		}
		else
		{
			if( !try_open_file( file_path, ref file_stream, ref offset, logger ) )
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
				logger.Invoke( $"Failed to query length of file '{file_path}': {exception.GetType()}: {exception.Message}" );
				file_stream.Close();
				file_stream = null;
				offset = 0;
				length = 0;
				return false;
			}
			return true;
		}
	}

	private static bool try_seek( FilePath file_path, ref SysIo.FileStream? file_stream, ref long offset, Procedure<string> logger )
	{
		if( !try_open_file( file_path, ref file_stream, ref offset, logger ) )
			return false;
		Assert( file_stream != null );
		try
		{
			file_stream.Seek( offset, SysIo.SeekOrigin.Begin );
		}
		catch( Sys.Exception exception )
		{
			logger.Invoke( $"Failed to seek in file '{file_path}': {exception.GetType()}: {exception.Message}" );
			file_stream.Close();
			file_stream = null;
			return false;
		}
		return true;
	}

	private static (byte[], int)? try_read( FilePath file_path, ref SysIo.FileStream? file_stream, ref long offset, int count, Procedure<string> logger )
	{
		if( !try_seek( file_path, ref file_stream, ref offset, logger ) )
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
			logger.Invoke( $"Failed to read from file '{file_path}': {exception.GetType()}: {exception.Message}" );
			file_stream.Close();
			file_stream = null;
			return null;
		}
	}
}
