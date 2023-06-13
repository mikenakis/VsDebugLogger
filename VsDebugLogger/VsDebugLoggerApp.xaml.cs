namespace VsDebugLogger;

using Sys = global::System;
using SysIo = global::System.IO;
using SysIoPipes = global::System.IO.Pipes;
using SysTasks = global::System.Threading.Tasks;
using SysGlob = global::System.Globalization;
using Wpf = System.Windows;
using WpfThread = System.Windows.Threading;
using global::System.Collections.Generic;
using static global::Framework.Statics;
using Log = global::Framework.Logging.Log;
using Framework.Extensions;
using Framework;
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

	private TheApp? theApp;

	protected override void OnStartup( Wpf.StartupEventArgs startupEventArgs )
	{
		base.OnStartup( startupEventArgs );

		Assert( theApp == null );
		try
		{
			theApp = TheApp.Create( startupEventArgs.Args );
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

	protected override void OnExit( Wpf.ExitEventArgs exitEventArgs )
	{
		theApp?.Dispose();
		theApp = null;
		if( DebugMode )
			DotNetHelpers.PerformGarbageCollection();
	}
}

public class TheApp : Sys.IDisposable
{
	public static TheApp Create( string[] commandLineArguments )
	{
		if( NamedPipeServer.IsAlreadyRunning() )
		{
			bring_other_instance_to_foreground();
			throw new AlreadyRunningApplicationException();
		}
		return new TheApp( commandLineArguments );
	}

	private static readonly Sys.TimeSpan defaultInterval = Sys.TimeSpan.FromSeconds( 0.2 );
	private readonly LifeGuard lifeGuard = LifeGuard.Create();
	private readonly NamedPipeServer namedPipeServer;

	//PEARL: by default, WPF DispatcherTimer runs at background priority. If you want normal priority, you have to explicitly specify it.
	private readonly WpfThread.DispatcherTimer wpfTimer;

	private TheApp( string[] commandLineArguments )
	{
		Wpf.Application.Current.MainWindow = new VsDebugLoggerMainWindow();
		Wpf.Application.Current.MainWindow.Show();

		namedPipeServer = NamedPipeServer.Create( new_session_handler );

		CommandlineArgumentParser commandlineArgumentParser = new CommandlineArgumentParser( commandLineArguments );

		string intervalAsString = commandlineArgumentParser.ExtractOption( "interval", defaultInterval.TotalSeconds.ToString( SysGlob.CultureInfo.InvariantCulture ) );
		const SysGlob.NumberStyles options = SysGlob.NumberStyles.Float | SysGlob.NumberStyles.AllowExponent | SysGlob.NumberStyles.AllowDecimalPoint;
		if( !double.TryParse( intervalAsString, options, SysGlob.NumberFormatInfo.InvariantInfo, out double intervalAsSeconds ) )
			throw new Sys.ApplicationException( $"Expected a (fractional) number of seconds, got '{intervalAsString}'." );
		Sys.TimeSpan interval = Sys.TimeSpan.FromSeconds( intervalAsSeconds );
		Log.Info( $"Polling every {interval.TotalSeconds} seconds" );

		if( commandlineArgumentParser.NonEmpty )
			Log.Warn( $"Superfluous command line arguments: {commandlineArgumentParser.AllRemainingArguments}" );

		wpfTimer = new WpfThread.DispatcherTimer( WpfThread.DispatcherPriority.Normal );
		wpfTimer.Interval = interval;
		wpfTimer.Tick += on_timer_tick;
		wpfTimer.Start();
	}

	public void Dispose()
	{
		lifeGuard.Dispose();
		namedPipeServer.Dispose();
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
		Assert( sender == wpfTimer );
		Assert( wpfTimer.IsEnabled );

		try
		{
			foreach( var sessionHandler in sessionHandlers )
				sessionHandler.Tick();
		}
		catch( Sys.Exception exception )
		{
			Log.Error( $"Unexpected exception: {exception.GetType()}: {exception.Message}" );
		}
	}

	private static void bring_other_instance_to_foreground()
	{
		using( var namedPipeClientStream = new SysIoPipes.NamedPipeClientStream( ".", "VsDebugLogger", SysIoPipes.PipeDirection.InOut, SysIoPipes.PipeOptions.None ) )
		{
			namedPipeClientStream.Connect( 1000 );
			SysIo.StreamWriter writer = new SysIo.StreamWriter( namedPipeClientStream );
			writer.WriteLine( "Activate" );
			writer.Flush();
		}
	}

	private readonly List<MySession> sessionHandlers = new();

	internal void AddSession( MySession mySession )
	{
		sessionHandlers.Add( mySession );
	}

	internal void RemoveSession( MySession mySession )
	{
		sessionHandlers.DoRemove( mySession );
	}

	internal abstract class MySession : NamedPipeServer.Session
	{
		protected readonly TheApp TheApp;

		protected MySession( TheApp theApp )
		{
			TheApp = theApp;
			theApp.AddSession( this );
		}

		public abstract void LineReceived( string line );

		public virtual SysTasks.ValueTask DisposeAsync()
		{
			TheApp.RemoveSession( this );
			return SysTasks.ValueTask.CompletedTask;
		}

		public abstract void Tick();
	}

	internal sealed class ActivationSession : MySession
	{
		public ActivationSession( TheApp theApp, List<string> parameters )
				: base( theApp )
		{
			if( parameters.Count != 0 )
				Log.Debug( $"Unexpected parameters received: {parameters.MakeString( " " )}" );
			Wpf.Application.Current.MainWindow!.Activate();
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
		public static LogFileSession Create( TheApp theApp, List<string> parameters )
		{
			CommandlineArgumentParser commandlineArgumentParser = new CommandlineArgumentParser( parameters );
			bool skipExisting = commandlineArgumentParser.ExtractSwitch( "skip_existing" );
			string filePathAsString = commandlineArgumentParser.ExtractOption( "file" );
			if( !SysIo.Path.IsPathFullyQualified( filePathAsString ) )
				throw new Sys.ApplicationException( $"Expected a fully qualified pathname, got '{filePathAsString}'." );
			FilePath filePath = FilePath.FromAbsolutePath( filePathAsString );
			string solutionName = commandlineArgumentParser.ExtractOption( "solution", "" );
			return new LogFileSession( theApp, filePath, solutionName, skipExisting );
		}

		private readonly ResilientVsDebugProxy debugPane;
		private readonly ResilientInputStream resilientInputStream;
		private readonly FilePath filePath;
		private readonly string solutionName;

		private LogFileSession( TheApp theApp, FilePath filePath, string solutionName, bool skipExisting )
				: base( theApp )
		{
			this.filePath = filePath;
			this.solutionName = solutionName;
			Log.Info( "Session established." );
			Log.Info( $"Reading from '{filePath}'" );
			Log.Info( $"Appending to the debug output window of solution '{solutionName}'." );
			debugPane = new ResilientVsDebugProxy( solutionName );
			resilientInputStream = new ResilientInputStream( filePath, skipExisting );
		}

		public override void LineReceived( string line )
		{
			Log.Error( $"Unexpected line received: {line}" );
		}

		public override async SysTasks.ValueTask DisposeAsync()
		{
			Log.Info( "Session ended." );
			await SysTasks.Task.Delay( Sys.TimeSpan.FromSeconds( 5.0 ) );
			await base.DisposeAsync();
		}

		public override void Tick()
		{
			resilientInputStream.ReadNext( text =>
				{
					bool ok = debugPane.Write( text );
					if( ok )
						Log.Debug( $"wrote {text.Length} characters." );
					return ok;
				} );
		}

		public override string ToString() => $"{filePath} -> {solutionName}";
	}
}
