namespace VsDebugLogger;

using Framework;
using Wpf = Sys.Windows;
using static Framework.Statics;
using SysIoPipes = SysIo.Pipes;
using WpfThread = Sys.Windows.Threading;
using Framework.FileSystem;

public partial class VsDebugLoggerApp //: Wpf.Application
{
	public VsDebugLoggerApp()
	{
		Sys.AppDomain.CurrentDomain.ProcessExit += static ( sender, _ ) =>
			{
				Assert( ReferenceEquals( sender, Sys.AppDomain.CurrentDomain ) );
				//Assert( e == null ); //PEARL: on DotNet Framework, this is always null. on NetCore, this is not null.
				Log.Debug( "ProcessExit" );
			};
		Sys.AppDomain.CurrentDomain.UnhandledException += static ( _, e ) =>
			{
				//Assert( ReferenceEquals( sender, Sys.AppDomain.CurrentDomain ) ); //PEARL: on DotNet Framework, sender is equal to Sys.AppDomain.CurrentDomain. On NetCore, sender is null.
				string message = e.ExceptionObject is Sys.Exception exception ? exception.Message : NotNull( e.ExceptionObject.ToString() );
				Log.Error( $"Unhandled Exception! (terminating={e.IsTerminating}) {e.ExceptionObject.GetType().Name} : {message}" );
			};
	}

	private TheApp? the_app;

	protected override void OnStartup( Wpf.StartupEventArgs startup_event_args )
	{
		base.OnStartup( startup_event_args );

		Assert( the_app == null );
		try
		{
			the_app = TheApp.Create( startup_event_args.Args );
		}
		catch( Sys.ApplicationException exception )
		{
			Log.Error( exception.Message );
			Shutdown( -1 );
		}
		catch( Sys.Exception exception )
		{
			Log.Error( "Application startup failed", exception );
			Shutdown( -1 );
		}
	}

	protected override void OnExit( Wpf.ExitEventArgs exit_event_args )
	{
		the_app?.Dispose();
		the_app = null;
		if( DebugMode )
			DotNetHelpers.PerformGarbageCollection();
	}
}

public class TheApp : Sys.IDisposable
{
	public static TheApp Create( string[] command_line_arguments )
	{
		if( NamedPipeServer.IsAlreadyRunning() )
		{
			bring_other_instance_to_foreground();
			throw new AlreadyRunningApplicationException();
		}
		return new TheApp( command_line_arguments );
	}

	private static readonly Sys.TimeSpan default_interval = Sys.TimeSpan.FromSeconds( 1.0 );
	private readonly LifeGuard life_guard = LifeGuard.Create();
	private readonly NamedPipeServer named_pipe_server;

	//PEARL: by default, WPF DispatcherTimer runs at background priority. If you want normal priority, you have to explicitly specify it.
	private readonly WpfThread.DispatcherTimer wpf_timer;

	private TheApp( string[] command_line_arguments )
	{
		Wpf.Application.Current.MainWindow = new VsDebugLoggerMainWindow();
		Wpf.Application.Current.MainWindow.Show();

		named_pipe_server = NamedPipeServer.Create( new_session_handler );

		CommandlineArgumentParser commandline_argument_parser = new CommandlineArgumentParser( command_line_arguments );

		string interval_as_string = commandline_argument_parser.ExtractOption( "interval", default_interval.TotalSeconds.ToString( SysGlob.CultureInfo.InvariantCulture ) );
		const SysGlob.NumberStyles options = SysGlob.NumberStyles.Float | SysGlob.NumberStyles.AllowExponent | SysGlob.NumberStyles.AllowDecimalPoint;
		if( !double.TryParse( interval_as_string, options, SysGlob.NumberFormatInfo.InvariantInfo, out double interval_as_seconds ) )
			throw new Sys.ApplicationException( $"Expected a (fractional) number of seconds, got '{interval_as_string}'." );
		Sys.TimeSpan interval = Sys.TimeSpan.FromSeconds( interval_as_seconds );
		Log.Info( $"Polling every {interval.TotalSeconds} seconds" );

		if( commandline_argument_parser.NonEmpty )
			Log.Warn( $"Superfluous command line arguments: {commandline_argument_parser.AllRemainingArguments}" );

		wpf_timer = new WpfThread.DispatcherTimer( WpfThread.DispatcherPriority.Normal );
		wpf_timer.Interval = interval;
		wpf_timer.Tick += on_timer_tick;
		wpf_timer.Start();
	}

	public void Dispose()
	{
		life_guard.Dispose();
		named_pipe_server.Dispose();
	}

	private NamedPipeServer.Session new_session_handler( string verb, List<string> parameters )
	{
		return verb switch
		{
			"Activate" => new ActivationSession( this, parameters ),
			"LogFile" => LogFileSession.Create( this, parameters ),
			_ => throw new Sys.ApplicationException( $"Unknown verb: {verb}" )
		};
	}

	private void on_timer_tick( object? sender, Sys.EventArgs e )
	{
		Assert( sender == wpf_timer );
		Assert( wpf_timer.IsEnabled );

		try
		{
			foreach( var session_handler in session_handlers )
				session_handler.Tick();
		}
		catch( Sys.Exception exception )
		{
			Log.Error( $"Unexpected exception: {exception.GetType()}: {exception.Message}" );
		}
	}

	private static void bring_other_instance_to_foreground()
	{
		using( var named_pipe_client_stream = new SysIoPipes.NamedPipeClientStream( ".", "VsDebugLogger", SysIoPipes.PipeDirection.InOut, SysIoPipes.PipeOptions.None ) )
		{
			named_pipe_client_stream.Connect( 1000 );
			SysIo.StreamWriter writer = new SysIo.StreamWriter( named_pipe_client_stream );
			writer.WriteLine( "Activate" );
			writer.Flush();
		}
	}

	private readonly List<MySession> session_handlers = new();

	internal void AddSession( MySession my_session )
	{
		session_handlers.Add( my_session );
	}

	internal void RemoveSession( MySession my_session )
	{
		session_handlers.DoRemove( my_session );
	}

	internal abstract class MySession : NamedPipeServer.Session
	{
		protected readonly TheApp TheApp;

		protected MySession( TheApp the_app )
		{
			TheApp = the_app;
			the_app.AddSession( this );
		}

		public abstract void LineReceived( string line );

		public virtual void Dispose()
		{
			TheApp.RemoveSession( this );
		}

		public abstract void Tick();
	}

	internal sealed class ActivationSession : MySession
	{
		public ActivationSession( TheApp the_app, List<string> parameters )
				: base( the_app )
		{
			if( parameters.Count != 0 )
				Log.Debug( $"Unexpected parameters received: {parameters.MakeString( " " )}" );
			Wpf.Application.Current.MainWindow!.Activate();
			Log.Info( "Activated." );
		}

		public override void LineReceived( string line )
		{
			Log.Debug( $"Unexpected line received: {line}" );
		}

		public override void Tick()
		{
			Log.Debug( "Unexpected tick received." );
		}
	}

	internal sealed class LogFileSession : MySession
	{
		public static LogFileSession Create( TheApp the_app, List<string> parameters )
		{
			CommandlineArgumentParser commandline_argument_parser = new CommandlineArgumentParser( parameters );

			string file_path_as_string = commandline_argument_parser.ExtractOption( "file" );
			if( !SysIo.Path.IsPathFullyQualified( file_path_as_string ) )
				throw new Sys.ApplicationException( $"Expected a fully qualified pathname, got '{file_path_as_string}'." );
			FilePath file_path = FilePath.FromAbsolutePath( file_path_as_string );
			string solution_name = commandline_argument_parser.ExtractOption( "solution", "" );
			return new LogFileSession( the_app, file_path, solution_name );
		}

		private readonly ResilientVsDebugProxy debug_pane;
		private readonly ResilientInputStream resilient_input_stream;
		private readonly FilePath file_path;
		private readonly string solution_name;

		private LogFileSession( TheApp the_app, FilePath file_path, string solution_name )
				: base( the_app )
		{
			this.file_path = file_path;
			this.solution_name = solution_name;
			Log.Info( "Session established." );
			Log.Info( $"Reading from '{file_path}'" );
			Log.Info( $"Appending to the debug output window of solution '{solution_name}'." );
			debug_pane = new ResilientVsDebugProxy( solution_name );
			resilient_input_stream = new ResilientInputStream( file_path );
		}

		public override void LineReceived( string line )
		{
			Log.Debug( $"Line received: {line}" );
			if( line == "Activate" )
				Wpf.Application.Current.MainWindow!.Activate();
		}

		public override void Dispose()
		{
			Log.Info( "Session ended." );
			base.Dispose();
		}

		public override void Tick()
		{
			//Log.Debug( $"Tick" );
			if( !resilient_input_stream.ReadNext( out string text ) )
				return;
			if( text == "" )
				return;
			if( !debug_pane.Write( text ) )
				return;
			//log( "Appended text." );
		}
	}
}
