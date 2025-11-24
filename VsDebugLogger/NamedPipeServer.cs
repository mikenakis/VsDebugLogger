namespace VsDebugLogger;

using global::System.Collections.Generic;
using global::System.Linq;
using VsDebugLoggerKit;
using static global::VsDebugLoggerKit.Statics;
using Log = global::VsDebugLoggerKit.Logging.Log;
using Sys = global::System;
using SysIo = global::System.IO;
using SysIoPipes = System.IO.Pipes;
using SysTasks = global::System.Threading.Tasks;

internal sealed class NamedPipeServer : Sys.IDisposable
{
	public const int MaxInstanceCount = 10;
	private static readonly string pipeName = DotNetHelpers.MainModuleName;

	public delegate Session SessionFactory( string verb, List<string> parameters );

	public interface Session : /*Sys.IDisposable,*/ Sys.IAsyncDisposable
	{
		void LineReceived( string line );
	}

	public static bool IsAlreadyRunning()
	{
		SysIoPipes.NamedPipeServerStream namedPipeServer;
		try
		{
			namedPipeServer = new SysIoPipes.NamedPipeServerStream( pipeName, SysIoPipes.PipeDirection.InOut, MaxInstanceCount );
		}
		catch( SysIo.IOException ioException ) when( unchecked((uint)ioException.HResult) == 0x800700E7 ) //"All pipe instances are busy"
		{
			return true;
		}
		namedPipeServer.Dispose();
		return false;
	}

	public static NamedPipeServer Create( SessionFactory sessionFactory )
	{
		return new NamedPipeServer( sessionFactory );
	}

	private readonly StatefulLifeGuard lifeGuard = StatefulLifeGuard.Create( true );

	// ReSharper disable once CollectionNeverQueried.Local
	private readonly List<SysTasks.Task> tasks = new();

	private NamedPipeServer( SessionFactory sessionFactory )
	{
		for( int i = 0; i < MaxInstanceCount; i++ )
		{
			SysTasks.Task task = run_named_pipe_server( i, pipeName, sessionFactory, () => !lifeGuard.IsAlive );
			tasks.Add( task );
		}
		Log.Info( $"Named pipe server '{pipeName}' is running." );
	}

	private static async SysTasks.Task run_named_pipe_server( int instanceNumber, string pipeName, SessionFactory sessionFactory, Function<bool> quit )
	{
		try
		{
			while( true )
			{
				SysIoPipes.NamedPipeServerStream namedPipeServer = new SysIoPipes.NamedPipeServerStream( pipeName, SysIoPipes.PipeDirection.InOut, MaxInstanceCount );
				Log.Debug( $"instance {instanceNumber} NamedPipeServer waiting for a session..." );
				await namedPipeServer.WaitForConnectionAsync();
				if( quit.Invoke() )
				{
					Log.Debug( $"instance {instanceNumber} Quits." );
					break;
				}
				Log.Debug( $"instance {instanceNumber} NamedPipeServer session established." );
				try
				{
					// SysIo.StreamWriter writer = new SysIo.StreamWriter( named_pipe_server );
					// writer.AutoFlush = true;
					using( SysIo.StreamReader reader = new SysIo.StreamReader( namedPipeServer ) )
					{
						string? firstLine = await reader.ReadLineAsync();
						if( firstLine == null )
							throw new Sys.ApplicationException( "Nothing received" );
						Log.Debug( $"instance {instanceNumber} Session: first_line: {firstLine}" );
						string[] parts = firstLine.Split( " ", Sys.StringSplitOptions.RemoveEmptyEntries | Sys.StringSplitOptions.TrimEntries );
						if( parts.Length < 1 )
							throw new Sys.ApplicationException( "Malformed request" );
						string verb = parts[0];
						List<string> parameters = parts.Skip( 1 ).ToList();
						await using( Session session = sessionFactory.Invoke( verb, parameters ) )
						{
							while( true )
							{
								string? line = await reader.ReadLineAsync();
								if( line == null )
									break;
								Log.Debug( $"instance {instanceNumber} Session: {line}" );
								session.LineReceived( line );
							}
							Log.Debug( $"instance {instanceNumber} Session: end-of-stream" );
						}
					}
				}
				catch( Sys.Exception exception )
				{
					Log.Error( $"instance {instanceNumber} Session failed", exception );
				}
				Log.Debug( $"instance {instanceNumber} NamedPipeServer session ended." );
				await namedPipeServer.DisposeAsync();
			}
		}
		catch( Sys.Exception exception )
		{
			Log.Error( $"instance {instanceNumber} Server failed", exception );
		}
		// ReSharper disable once FunctionNeverReturns
	}

	public void Dispose()
	{
		lifeGuard.Dispose();

		// the following does not work, I am not sure why.
		if( False )
		{
			// create one fake client connection per server instance so as to make each server instance move past `WaitForConnection()`
			for( int i = 0; i < MaxInstanceCount; i++ )
				using( SysIoPipes.NamedPipeClientStream namedPipeClientStream = new SysIoPipes.NamedPipeClientStream( pipeName ) )
					namedPipeClientStream.Connect( 100 );
		}
		// the following cannot be done because it throws.
		// foreach( var task in tasks )
		// 	task.Dispose();
	}
}
