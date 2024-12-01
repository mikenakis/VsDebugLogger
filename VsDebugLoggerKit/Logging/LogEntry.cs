namespace VsDebugLoggerKit.Logging;

using Sys = System;
using System.Collections.Generic;

public class LogEntry
{
	public LogLevel Level { get; }
	public Sys.DateTime Utc { get; }
	public string Message { get; }
	public string SourceFileName { get; }
	public int SourceLineNumber { get; }

	public LogEntry( LogLevel level, Sys.DateTime utc, string message, string sourceFileName, int sourceLineNumber )
	{
		Level = level;
		Utc = utc;
		Message = message;
		SourceFileName = sourceFileName;
		SourceLineNumber = sourceLineNumber;
	}

	public override string ToString()
	{
		return $"level={Level}; utc={Utc}; message={Message}; sourceFileName={SourceFileName}; sourceLineNumber={SourceLineNumber}";
	}

	public IReadOnlyList<string> ToStrings()
	{
		Sys.DateTime t = Utc.ToLocalTime();
		return new[]
			{
				$"{SourceFileName}({SourceLineNumber}): ", //
				$"{StringFromLogLevel( Level )}", //
				$" | {t.Year:D4}-{t.Month:D2}-{t.Day:D2} {t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}.{t.Millisecond:D3} | ", //
				Message
			};
	}

	public static string StringFromLogLevel( LogLevel level )
	{
		return level switch
		{
			LogLevel.Debug => "DEBUG",
			LogLevel.Info => "INFO ",
			LogLevel.Warn => "WARN ",
			LogLevel.Error => "ERROR",
			_ => "unknown:" + level
		};
	}
}