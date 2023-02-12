namespace VsDebugLogger;

using Framework;
using System.Windows.Shapes;
using SysIoPipes = SysIo.Pipes;

internal sealed class NamedPipeServer : Sys.IDisposable
{
	public const int MaxInstanceCount = 10;
	private static readonly string pipe_name = DotNetHelpers.MainModuleName;

	public delegate Session SessionFactory( string verb, List<string> parameters );

	public interface Session : Sys.IDisposable
	{
		void LineReceived( string line );
	}

	public static bool IsAlreadyRunning()
	{
		SysIoPipes.NamedPipeServerStream named_pipe_server;
		try
		{
			named_pipe_server = new SysIoPipes.NamedPipeServerStream( pipe_name, SysIoPipes.PipeDirection.InOut, MaxInstanceCount );
		}
		catch( SysIo.IOException io_exception ) when( unchecked((uint)io_exception.HResult) == 0x800700E7 ) //"All pipe instances are busy"
		{
			return true;
		}
		named_pipe_server.Dispose();
		return false;
	}

	public static NamedPipeServer Create( SessionFactory session_factory )
	{
		List<SysTasks.Task> tasks = new();
		for( int i = 0; i < MaxInstanceCount; i++ )
		{
			SysTasks.Task task = run_named_pipe_server( i, pipe_name, session_factory );
			tasks.Add( task );
		}
		return new NamedPipeServer( tasks );
	}

	private readonly LifeGuard life_guard = LifeGuard.Create( true );

	// ReSharper disable once CollectionNeverQueried.Local
	private readonly List<SysTasks.Task> tasks;

	private NamedPipeServer( List<SysTasks.Task> tasks )
	{
		this.tasks = tasks;
		Log.Info( $"Named pipe server {pipe_name} is running." );
	}

	private static async SysTasks.Task run_named_pipe_server( int instance_number, string pipe_name, SessionFactory session_factory )
	{
		try
		{
			for( ;; )
			{
				SysIoPipes.NamedPipeServerStream named_pipe_server = new SysIoPipes.NamedPipeServerStream( pipe_name, SysIoPipes.PipeDirection.InOut, MaxInstanceCount );
				Log.Debug( $"instance {instance_number} NamedPipeServer waiting for a session..." );
				await named_pipe_server.WaitForConnectionAsync();
				Log.Debug( $"instance {instance_number} NamedPipeServer session established." );
				try
				{
					// SysIo.StreamWriter writer = new SysIo.StreamWriter( named_pipe_server );
					// writer.AutoFlush = true;
					using( SysIo.StreamReader reader = new SysIo.StreamReader( named_pipe_server ) )
					{
						string? first_line = await reader.ReadLineAsync();
						if( first_line == null )
							throw new Sys.ApplicationException( "Nothing received" );
						Log.Debug( $"instance {instance_number} Session: first_line: {first_line}" );
						string[] parts = first_line.Split( " ", Sys.StringSplitOptions.RemoveEmptyEntries | Sys.StringSplitOptions.TrimEntries );
						if( parts.Length < 1 )
							throw new Sys.ApplicationException( "Malformed request" );
						string verb = parts[0];
						List<string> parameters = parts.Skip( 1 ).ToList();
						using( Session session = session_factory.Invoke( verb, parameters ) )
							for( ;; )
							{
								string? line = await reader.ReadLineAsync();
								if( line == null )
								{
									Log.Debug( $"instance {instance_number} Session: end-of-stream" );
									break;
								}
								Log.Debug( $"instance {instance_number} Session: {line}" );
								session.LineReceived( line );
							}
					}
				}
				catch( Sys.Exception exception )
				{
					Log.Error( $"instance {instance_number} Session failed", exception );
				}
				Log.Debug( $"instance {instance_number} NamedPipeServer session ended." );
				// try
				// {
				// 	named_pipe_server.Disconnect();
				// }
				// catch( Sys.Exception exception )
				// {
				// 	Log.Debug( $"named_pipe_server.Disconnect() failed with {exception.GetType()}: {exception.Message}" );
				// }
				await named_pipe_server.DisposeAsync();
			}
		}
		catch( Sys.Exception exception )
		{
			Log.Error( $"instance {instance_number} Server failed", exception );
		}
		// ReSharper disable once FunctionNeverReturns
	}

	// private static NamedPipeServer start_named_pipe_server( Function<Session> session_factory )
	// {
	// 	string pipe_name = DotNetHelpers.MainModuleName;
	// 	NamedPipeServer named_pipe_server;
	// 	try
	// 	{
	// 		named_pipe_server = NamedPipeServer.Create( pipe_name );
	// 	}
	// 	catch( SysIo.IOException io_exception ) when( unchecked((uint)io_exception.HResult) == 0x800700E7 ) //"All pipe instances are busy"
	// 	{
	// 		throw new Sys.ApplicationException( "The application is already running." );
	// 	}
	// 	Log.Info( $"Named pipe server {pipe_name} running." );
	// 	Task task = run_named_pipe_server( named_pipe_server, session_factory );
	// 	return named_pipe_server;
	// }

	// private static async Task run_named_pipe_server( NamedPipeServer named_pipe_server, Function<Session> session_factory )
	// {
	// 	for( ;; )
	// 	{
	// 		Log.Debug( "NamedPipeServer waiting for a session..." );
	// 		await named_pipe_server.Start();
	// 		Log.Debug( "NamedPipeServer session established." );
	// 		try
	// 		{
	// 			using( SysIo.StreamReader reader = new SysIo.StreamReader( named_pipe_server.Stream ) )
	// 			{
	// 				await using( SysIo.StreamWriter writer = new SysIo.StreamWriter( named_pipe_server.Stream ) )
	// 				{
	// 					writer.AutoFlush = true;
	// 					using( Session session = session_factory.Invoke() )
	// 					{
	// 						for( ;; )
	// 						{
	// 							string? line = await reader.ReadLineAsync();
	// 							if( line == null )
	// 								break;
	// 							session.LineReceived( line );
	// 						}
	// 					}
	// 					Log.Debug( "NamedPipeServer session ended." );
	// 				}
	// 			}
	// 			named_pipe_server.Stop();
	// 		}
	// 		catch( Sys.Exception exception )
	// 		{
	// 			Log.Error( "Failed", exception.Message );
	// 		}
	// 	}
	// 	// Assert( false ); //The Async Named Pipe Server has completed; this is not supposed to happen.
	// 	// named_pipe_server.Dispose();
	// }

	public void Dispose()
	{
		life_guard.Dispose();
		foreach( var task in tasks )
			task.Dispose(); //does this cancel tasks?
			//named_pipe_server.Dispose();
	}
}
