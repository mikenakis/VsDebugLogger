namespace VsDebugLogger.Framework.Logging;

public abstract class Logger
{
	public static Logger Instance = DebugLogger.Instance; //by default, we only have a debug logger; the application may replace this with a more elaborate logger.

	public abstract void AddLogEntry( LogEntry log_entry );
}
