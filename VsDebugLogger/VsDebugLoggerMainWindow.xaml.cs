namespace VsDebugLogger;

using Framework.Logging;

public partial class VsDebugLoggerMainWindow //: Wpf.Window
{
	public VsDebugLoggerMainWindow()
	{
		InitializeComponent();
		GlobalLogger.Instance = DistributingLogger.Of( DebugLogger.Instance, log );
	}

	private void log( LogEntry log_entry )
	{
		if( log_entry.Level == LogLevel.Debug )
			return;
		string text = log_entry.Level + ": " + log_entry.Message + "\r\n";
		StatusText.Text += text;
		StatusText.ScrollToEnd();
	}
}
