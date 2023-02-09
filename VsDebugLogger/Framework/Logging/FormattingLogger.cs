namespace VsDebugLogger.Framework.Logging;

using System.Collections.Generic;
using SysText = System.Text;
using SysThread = System.Threading;

public class FormattingLogger : Logger
{
	private readonly Procedure<string> log_line_consumer;
	private int longest_first_part_length;

	public FormattingLogger( Procedure<string> log_line_consumer )
	{
		this.log_line_consumer = log_line_consumer;
	}

	public override void AddLogEntry( LogEntry log_entry )
	{
		IReadOnlyList<string> parts = log_entry.ToStrings();
		SysText.StringBuilder string_builder = new SysText.StringBuilder();
		for( int i = 0; i < parts.Count; i++ )
		{
			string_builder.Append( parts[i] );
			if( i == 0 )
			{
				while( parts[i].Length > longest_first_part_length )
					SysThread.Interlocked.Increment( ref longest_first_part_length );
				string_builder.Append( new string( ' ', longest_first_part_length - parts[i].Length ) );
			}
		}
		string text = string_builder.ToString();
		log_line_consumer.Invoke( text );
	}
}
