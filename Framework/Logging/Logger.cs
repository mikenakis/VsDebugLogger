namespace Framework.Logging;

public delegate void Logger( LogEntry log_entry );

public static class GlobalLogger
{
	public static Logger Instance = DebugLogger.Instance; //by default, we only have a debug logger; the application may replace this with a more elaborate logger.
}