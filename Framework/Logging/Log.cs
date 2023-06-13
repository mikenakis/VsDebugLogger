namespace Framework.Logging;

using Sys = global::System;
using SysText = global::System.Text;
using SysDiag = global::System.Diagnostics;
using SysCompiler = global::System.Runtime.CompilerServices;
using SysReflect = System.Reflection;
using global::System.Collections.Generic;
using global::System.Linq;
using static global::Framework.Statics;
using Framework.Extensions;

public static class Log
{
	//[Diagnostics.Conditional( "DEBUG" )]
	public static void Debug( string message, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) //
		=> fix_and_log_message( LogLevel.Debug, Sys.DateTime.UtcNow, message, sourceFileName, sourceLineNumber );

	public static void Info( string message, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) //
		=> fix_and_log_message( LogLevel.Info, Sys.DateTime.UtcNow, message, sourceFileName, sourceLineNumber );

	public static void Warn( string message, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) //
		=> fix_and_log_message( LogLevel.Warn, Sys.DateTime.UtcNow, message, sourceFileName, sourceLineNumber );

	public static void Error( string message, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) //
		=> fix_and_log_message( LogLevel.Error, Sys.DateTime.UtcNow, message, sourceFileName, sourceLineNumber );

	public static void MessageWithGivenLevel( LogLevel logLevel, string message, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) //
		=> fix_and_log_message( logLevel, Sys.DateTime.UtcNow, message, sourceFileName, sourceLineNumber );

	public static void MessageWithGivenLevelAndTime( LogLevel logLevel, Sys.DateTime utc, string message, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) //
		=> fix_and_log_message( logLevel, utc, message, sourceFileName, sourceLineNumber );

	public static void Warn( string prefix, Sys.Exception exception, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) //
		=> log_raw_message( LogLevel.Warn, Sys.DateTime.UtcNow, build_long_exception_message( prefix, exception ), sourceFileName, sourceLineNumber );

	public static void Error( string prefix, Sys.Exception exception, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) //
		=> log_raw_message( LogLevel.Error, Sys.DateTime.UtcNow, build_long_exception_message( prefix, exception ), sourceFileName, sourceLineNumber );

	public static void LogRawMessage( LogLevel logLevel, string message, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) //
		=> log_raw_message( logLevel, Sys.DateTime.UtcNow, message, sourceFileName, sourceLineNumber );

	///////////////////////////////////////////////////////////////////////////////////////////////////////////////

	private static void fix_and_log_message( LogLevel logLevel, Sys.DateTime utc, string message, string sourceFileName, int sourceLineNumber )
	{
		message = fix_message( message );
		log_raw_message( logLevel, utc, message, sourceFileName, sourceLineNumber );
	}

	private static void log_raw_message( LogLevel logLevel, Sys.DateTime utc, string message, string sourceFileName, int sourceLineNumber )
	{
		sourceFileName = fix_source_file_name( sourceFileName );
		LogEntry entry = new LogEntry( logLevel, utc, message, sourceFileName, sourceLineNumber );
		GlobalLogger.Instance.Invoke( entry );
	}

	private static string fix_message( string message )
	{
		message = message.Replace( '|', '¦' );
		message = message.Replace( "\r\n", " ¦ " );
		message = message.Replace( "\r", " ¦ " );
		message = message.Replace( "\n", " ¦ " );
		message = message.Replace( "\t", "    " );
		return message;
	}

	private static string build_long_exception_message( string prefix, Sys.Exception exception )
	{
		List<string> lines = new List<string>();
		recurse( $"{prefix} : ", lines, exception );
		return lines.Select( fix_message ).MakeString( "\r\n" );

		static void recurse( string prefix, List<string> lines, Sys.Exception exception )
		{
			for( ;; exception = exception.InnerException )
			{
				lines.Add( $"{prefix}{exception.GetType()} : {exception.Message}" );
				SysDiag.StackFrame[] stackFrames = new SysDiag.StackTrace( exception, true ).GetFrames();
				lines.AddRange( stackFrames.Select( string_from_stack_frame ) );
				if( exception is Sys.AggregateException aggregateException )
				{
					Assert( ReferenceEquals( exception.InnerException, aggregateException.InnerExceptions[0] ) );
					foreach( var innerException in aggregateException.InnerExceptions )
						recurse( "Aggregates ", lines, innerException );
					break;
				}
				if( exception.InnerException == null )
					break;
				prefix = "Caused by ";
			}
		}
	}

	private static string string_from_stack_frame( SysDiag.StackFrame stackFrame )
	{
		SysText.StringBuilder stringBuilder = new SysText.StringBuilder();
		stringBuilder.Append( "    " );
		string? sourceFileName = stackFrame.GetFileName();
		stringBuilder.Append( string.IsNullOrEmpty( sourceFileName ) ? "<unknown-source>: " : $"{fix_source_file_name( sourceFileName )}({stackFrame.GetFileLineNumber()}): " );
		SysReflect.MethodBase? method = stackFrame.GetMethod();
		if( method != null )
		{
			stringBuilder.Append( "method " );
			Sys.Type? declaringType = method.DeclaringType;
			if( declaringType != null )
				stringBuilder.Append( FrameworkHelpers.GetCSharpTypeName( declaringType ).Replace( '+', '.' ) ).Append( "." );
			stringBuilder.Append( method.Name );
			if( method is SysReflect.MethodInfo && method.IsGenericMethod )
				stringBuilder.Append( "<" ).Append( method.GetGenericArguments().Select( a => a.Name ).MakeString( "," ) ).Append( ">" );
			stringBuilder.Append( "(" ).Append( method.GetParameters().Select( p => p.ParameterType.Name + " " + p.Name ).MakeString( ", " ) ).Append( ")" );
		}
		return stringBuilder.ToString();
	}

	private static string fix_source_file_name( string sourceFileName )
	{
		if( True ) //XXX FIXME TODO the following code has been disabled because Visual Studio 17.4.4 fucks up. See https://stackoverflow.com/q/75224235/773113
			return sourceFileName;
		string solutionSourcePath = SolutionSourcePath.Value;
		if( !sourceFileName.StartsWith( solutionSourcePath, Sys.StringComparison.Ordinal ) )
			return sourceFileName;
		int start = solutionSourcePath.Length;
		while( start < sourceFileName.Length && (sourceFileName[start] == '\\' || sourceFileName[start] == '/') )
			start++;
		return sourceFileName[start..];
	}
}
