namespace VsDebugLogger;

using Framework;
using SysIoPipes = SysIo.Pipes;

internal sealed class NamedPipeServer : Sys.IDisposable
{
	public const int MaxInstanceCount = 10;
	private static readonly string pipe_name = DotNetHelpers.MainModuleName;

	public delegate Session SessionFactory( string verb, List<string> parameters );

	public interface Session : /*Sys.IDisposable,*/ Sys.IAsyncDisposable
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
		Log.Info( $"Named pipe server '{pipe_name}' is running." );
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
						await using( Session session = session_factory.Invoke( verb, parameters ) )
						{
							for( ;; )
							{
								string? line = await reader.ReadLineAsync();
								if( line == null )
									break;
								Log.Debug( $"instance {instance_number} Session: {line}" );
								session.LineReceived( line );
							}
							Log.Debug( $"instance {instance_number} Session: end-of-stream" );
						}
					}
				}
				catch( Sys.Exception exception )
				{
					Log.Error( $"instance {instance_number} Session failed", exception );
				}
				Log.Debug( $"instance {instance_number} NamedPipeServer session ended." );
				await named_pipe_server.DisposeAsync();
			}
		}
		catch( Sys.Exception exception )
		{
			Log.Error( $"instance {instance_number} Server failed", exception );
		}
		// ReSharper disable once FunctionNeverReturns
	}

	public void Dispose()
	{
		life_guard.Dispose();
		foreach( var task in tasks )
			task.Dispose(); //does this cancel tasks?
			//named_pipe_server.Dispose();
	}
}
