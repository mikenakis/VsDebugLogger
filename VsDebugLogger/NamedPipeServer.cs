namespace VsDebugLogger;

using Framework;
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
		List<(SysTasks.Task, SysIoPipes.NamedPipeServerStream)> entries = new();
		for( int i = 0; i < MaxInstanceCount; i++ )
		{
			SysIoPipes.NamedPipeServerStream named_pipe_server = new SysIoPipes.NamedPipeServerStream( pipe_name, SysIoPipes.PipeDirection.InOut, MaxInstanceCount );
			SysTasks.Task task = run_named_pipe_server( named_pipe_server, session_factory );
			entries.Add( (task, named_pipe_server) );
		}
		return new NamedPipeServer( entries );
	}

	private readonly LifeGuard life_guard = LifeGuard.Create( true );

	// ReSharper disable once CollectionNeverQueried.Local
	private readonly List<(SysTasks.Task, SysIoPipes.NamedPipeServerStream)> entries;

	private NamedPipeServer( List<(SysTasks.Task, SysIoPipes.NamedPipeServerStream)> entries )
	{
		this.entries = entries;
		Log.Info( $"Named pipe server {pipe_name} is running." );
	}

	private static async SysTasks.Task run_named_pipe_server( SysIoPipes.NamedPipeServerStream named_pipe_server, SessionFactory session_factory )
	{
		for( ;; )
		{
			Log.Debug( "NamedPipeServer waiting for a session..." );
			await named_pipe_server.WaitForConnectionAsync();
			Log.Debug( "NamedPipeServer session established." );
			try
			{
				await handle_session( named_pipe_server, session_factory );
			}
			catch( Sys.Exception exception )
			{
				Log.Error( "Session failed", exception );
			}
			Log.Debug( "NamedPipeServer session ended." );
			named_pipe_server.Disconnect();
		}
		// ReSharper disable once FunctionNeverReturns
	}

	private static async SysTasks.Task handle_session( SysIo.Stream named_pipe_server, SessionFactory session_factory )
	{
		// SysIo.StreamWriter writer = new SysIo.StreamWriter( named_pipe_server );
		// writer.AutoFlush = true;
		using( SysIo.StreamReader reader = new SysIo.StreamReader( named_pipe_server ) )
		{
			string? first_line = await reader.ReadLineAsync();
			if( first_line == null )
				throw new Sys.ApplicationException( "Nothing received" );
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
						break;
					session.LineReceived( line );
				}
		}
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
		foreach( (_, var named_pipe_server) in entries )
			named_pipe_server.Dispose();
	}
}
