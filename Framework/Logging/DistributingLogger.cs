namespace Framework.Logging;

using System.Collections.Generic;
using static Statics;

public sealed class DistributingLogger
{
	public static Logger Of( params Logger[] loggers )
	{
		DistributingLogger distributing_logger = new DistributingLogger();
		foreach( var logger in loggers )
			distributing_logger.AddLog( logger );
		return distributing_logger.EntryPoint;
	}

	private readonly List<Logger> mutable_loggers = new();

	public void AddLog( Logger logger )
	{
		lock( mutable_loggers )
		{
			Assert( !mutable_loggers.Contains( logger ) );
			mutable_loggers.Add( logger );
		}
	}

	public void RemoveLog( Logger logger )
	{
		lock( mutable_loggers )
			mutable_loggers.DoRemove( logger );
	}

	private IReadOnlyList<Logger> get_loggers()
	{
		lock( mutable_loggers )
			return mutable_loggers.ToArray();
	}

	public Logger EntryPoint => add_log_entry;

	private void add_log_entry( LogEntry log_entry )
	{
		IReadOnlyList<Logger> loggers = get_loggers();
		Assert( loggers.Count > 0 );
		foreach( Logger logger in loggers )
		{
			try
			{
				logger.Invoke( log_entry );
			}
			catch( Sys.Exception exception )
			{
				foreach( var line in FrameworkHelpers.BuildMediumExceptionMessage( "Logger failed", exception ) )
					SysDiag.Debug.WriteLine( line );
			}
		}
	}
}