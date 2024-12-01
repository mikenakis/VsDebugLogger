namespace VsDebugLoggerKit.Logging;

using SysText = System.Text;
using SysThread = System.Threading;
using System.Collections.Generic;

public class FormattingLogger
{
	private readonly Procedure<string> logLineConsumer;
	private int longestFirstPartLength;

	public FormattingLogger( Procedure<string> logLineConsumer )
	{
		this.logLineConsumer = logLineConsumer;
	}

	public Logger EntryPoint => add_log_entry;

	private void add_log_entry( LogEntry logEntry )
	{
		IReadOnlyList<string> parts = logEntry.ToStrings();
		var stringBuilder = new SysText.StringBuilder();
		for( int i = 0; i < parts.Count; i++ )
		{
			stringBuilder.Append( parts[i] );
			if( i == 0 )
			{
				while( parts[i].Length > longestFirstPartLength )
					SysThread.Interlocked.Increment( ref longestFirstPartLength );
				stringBuilder.Append( new string( ' ', longestFirstPartLength - parts[i].Length ) );
			}
		}
		string text = stringBuilder.ToString();
		logLineConsumer.Invoke( text );
	}
}
