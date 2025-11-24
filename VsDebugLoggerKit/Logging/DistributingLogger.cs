namespace VsDebugLoggerKit.Logging;

using System.Collections.Generic;
using VsDebugLoggerKit;
using VsDebugLoggerKit.Extensions;
using static VsDebugLoggerKit.Statics;
using Sys = System;
using SysDiag = System.Diagnostics;

public sealed class DistributingLogger
{
	public static Logger Of( params Logger[] loggers )
	{
		var distributingLogger = new DistributingLogger();
		foreach( var logger in loggers )
			distributingLogger.AddLog( logger );
		return distributingLogger.EntryPoint;
	}

	private readonly List<Logger> mutableLoggers = new();

	public void AddLog( Logger logger )
	{
		lock( mutableLoggers )
		{
			Assert( !mutableLoggers.Contains( logger ) );
			mutableLoggers.Add( logger );
		}
	}

	public void RemoveLog( Logger logger )
	{
		lock( mutableLoggers )
			mutableLoggers.DoRemove( logger );
	}

	private IReadOnlyList<Logger> get_loggers()
	{
		lock( mutableLoggers )
			return mutableLoggers.ToArray();
	}

	public Logger EntryPoint => add_log_entry;

	private void add_log_entry( LogEntry logEntry )
	{
		IReadOnlyList<Logger> loggers = get_loggers();
		Assert( loggers.Count > 0 );
		foreach( Logger logger in loggers )
		{
			try
			{
				logger.Invoke( logEntry );
			}
			catch( Sys.Exception exception )
			{
				foreach( var line in FrameworkHelpers.BuildMediumExceptionMessage( "Logger failed", exception ) )
					SysDiag.Debug.WriteLine( line );
			}
		}
	}
}