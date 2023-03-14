namespace VsDebugLogger;

using Framework.Logging;

public partial class VsDebugLoggerMainWindow //: Wpf.Window
{
	public VsDebugLoggerMainWindow()
	{
		InitializeComponent();
		GlobalLogger.Instance = DistributingLogger.Of( DebugLogger.Instance, log );
	}

	private void log( LogEntry logEntry )
	{
		if( logEntry.Level == LogLevel.Debug )
			return;
		string text = logEntry.Level + ": " + logEntry.Message + "\r\n";
		StatusText.Text += text;
		StatusText.ScrollToEnd();
	}
}
