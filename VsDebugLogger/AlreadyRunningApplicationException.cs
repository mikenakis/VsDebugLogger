namespace VsDebugLogger;

internal sealed class AlreadyRunningApplicationException : Sys.ApplicationException
{
	public AlreadyRunningApplicationException()
			: base( "Application is already running." )
	{ }
}
