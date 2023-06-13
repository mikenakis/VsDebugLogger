namespace VsDebugLogger;

using Sys = global::System;

internal sealed class AlreadyRunningApplicationException : Sys.ApplicationException
{
	public AlreadyRunningApplicationException()
			: base( "Application is already running." )
	{ }
}
