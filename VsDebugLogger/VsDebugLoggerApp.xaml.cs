namespace VsDebugLogger;

using VsDebugLogger.Framework.Logging;
using Sys = System;
using Wpf = System.Windows;
using static Statics;

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

	protected override void OnStartup( Wpf.StartupEventArgs startup_event_args )
	{
		base.OnStartup( startup_event_args );

		MainWindow = new VsDebugLoggerMainWindow( startup_event_args.Args );
		MainWindow.Show();
	}
}
