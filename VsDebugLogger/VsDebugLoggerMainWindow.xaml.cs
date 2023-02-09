namespace VsDebugLogger;

using Sys = System;
using Wpf = System.Windows;

public partial class VsDebugLoggerMainWindow //: Wpf.Window
{
	private readonly string[] program_arguments;

	// ReSharper disable once NotAccessedField.Local
	private TheVsDebugLogger? vs_debug_logger = null;

	//TODO: select specific instance of visual studio
	//See https://stackoverflow.com/questions/14205933/how-do-i-get-the-dte-for-running-visual-studio-instance

	public VsDebugLoggerMainWindow( string[] program_arguments )
	{
		this.program_arguments = program_arguments;
		InitializeComponent();
		Loaded += window_loaded;
	}

	private string last_text = "";

	private void log( string text )
	{
		if( text == last_text )
			return;
		last_text = text;
		StatusText.Text += text + "\r\n";
		StatusText.ScrollToEnd();
	}

	private void window_loaded( object sender, Wpf.RoutedEventArgs routed_event_args )
	{
		try
		{
			vs_debug_logger = new TheVsDebugLogger( program_arguments, log );
		}
		catch( Sys.ApplicationException application_exception )
		{
			log( application_exception.Message );
		}
	}
}
