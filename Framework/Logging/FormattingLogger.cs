namespace Framework.Logging;

using System.Collections.Generic;
using SysText = SysText;
using SysThread = SysThread;

public class FormattingLogger
{
	private readonly Procedure<string> log_line_consumer;
	private int longest_first_part_length;

	public FormattingLogger( Procedure<string> log_line_consumer )
	{
		this.log_line_consumer = log_line_consumer;
	}

	public Logger EntryPoint => add_log_entry;

	private void add_log_entry( LogEntry log_entry )
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
