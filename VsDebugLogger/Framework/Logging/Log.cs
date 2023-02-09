namespace VsDebugLogger.Framework.Logging;

using System.Collections.Generic;
using System.Linq;
using Sys = System;
using SysCompiler = System.Runtime.CompilerServices;
using SysDiag = System.Diagnostics;
using SysText = System.Text;
using SysReflect = System.Reflection;
using static Statics;

public static class Log
{
	//[Diagnostics.Conditional( "DEBUG" )]
	public static void Debug( string message, [SysCompiler.CallerFilePath] string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 ) //
		=> fix_and_log_message( LogLevel.Debug, Sys.DateTime.UtcNow, message, source_file_name, source_line_number );

	public static void Info( string message, [SysCompiler.CallerFilePath] string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 ) //
		=> fix_and_log_message( LogLevel.Info, Sys.DateTime.UtcNow, message, source_file_name, source_line_number );

	public static void Warn( string message, [SysCompiler.CallerFilePath] string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 ) //
		=> fix_and_log_message( LogLevel.Warn, Sys.DateTime.UtcNow, message, source_file_name, source_line_number );

	public static void Error( string message, [SysCompiler.CallerFilePath] string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 ) //
		=> fix_and_log_message( LogLevel.Error, Sys.DateTime.UtcNow, message, source_file_name, source_line_number );

	public static void MessageWithGivenLevel( LogLevel log_level, string message, [SysCompiler.CallerFilePath] string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 ) //
		=> fix_and_log_message( log_level, Sys.DateTime.UtcNow, message, source_file_name, source_line_number );

	public static void MessageWithGivenLevelAndTime( LogLevel log_level, Sys.DateTime utc, string message, [SysCompiler.CallerFilePath] string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 ) //
		=> fix_and_log_message( log_level, utc, message, source_file_name, source_line_number );

	public static void Warn( string prefix, Sys.Exception exception, [SysCompiler.CallerFilePath] string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 ) //
		=> log_raw_message( LogLevel.Warn, Sys.DateTime.UtcNow, build_long_exception_message( prefix, exception ), source_file_name, source_line_number );

	public static void Error( string prefix, Sys.Exception exception, [SysCompiler.CallerFilePath] string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 ) //
		=> log_raw_message( LogLevel.Error, Sys.DateTime.UtcNow, build_long_exception_message( prefix, exception ), source_file_name, source_line_number );

	public static void LogRawMessage( LogLevel log_level, string message, [SysCompiler.CallerFilePath] string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 ) //
		=> log_raw_message( log_level, Sys.DateTime.UtcNow, message, source_file_name, source_line_number );

	///////////////////////////////////////////////////////////////////////////////////////////////////////////////

	private static void fix_and_log_message( LogLevel log_level, Sys.DateTime utc, string message, string source_file_name, int source_line_number )
	{
		message = fix_message( message );
		log_raw_message( log_level, utc, message, source_file_name, source_line_number );
	}

	private static void log_raw_message( LogLevel log_level, Sys.DateTime utc, string message, string source_file_name, int source_line_number )
	{
		source_file_name = fix_source_file_name( source_file_name );
		LogEntry entry = new LogEntry( log_level, utc, message, source_file_name, source_line_number );
		Logger.Instance.AddLogEntry( entry );
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
				SysDiag.StackFrame[] stack_frames = new SysDiag.StackTrace( exception, true ).GetFrames();
				lines.AddRange( stack_frames.Select( string_from_stack_frame ) );
				if( exception is Sys.AggregateException aggregate_exception )
				{
					Assert( ReferenceEquals( exception.InnerException, aggregate_exception.InnerExceptions[0] ) );
					foreach( var inner_exception in aggregate_exception.InnerExceptions )
						recurse( "Aggregates ", lines, inner_exception );
					break;
				}
				if( exception.InnerException == null )
					break;
				prefix = "Caused by ";
			}
		}
	}

	private static string string_from_stack_frame( SysDiag.StackFrame stack_frame )
	{
		SysText.StringBuilder string_builder = new SysText.StringBuilder();
		string_builder.Append( "    " );
		string? source_file_name = stack_frame.GetFileName();
		string_builder.Append( string.IsNullOrEmpty( source_file_name ) ? "<unknown-source>: " : $"{fix_source_file_name( source_file_name )}({stack_frame.GetFileLineNumber()}): " );
		SysReflect.MethodBase? method = stack_frame.GetMethod();
		if( method != null )
		{
			string_builder.Append( "method " );
			Sys.Type? declaring_type = method.DeclaringType;
			if( declaring_type != null )
				string_builder.Append( FrameworkHelpers.GetCSharpTypeName( declaring_type ).Replace( '+', '.' ) ).Append( "." );
			string_builder.Append( method.Name );
			if( method is SysReflect.MethodInfo && method.IsGenericMethod )
				string_builder.Append( "<" ).Append( method.GetGenericArguments().Select( a => a.Name ).MakeString( "," ) ).Append( ">" );
			string_builder.Append( "(" ).Append( method.GetParameters().Select( p => p.ParameterType.Name + " " + p.Name ).MakeString( ", " ) ).Append( ")" );
		}
		return string_builder.ToString();
	}

	private static string fix_source_file_name( string source_file_name )
	{
		// if( True ) //XXX FIXME TODO the following code has been disabled because Visual Studio 17.4.4 fucks up. See https://stackoverflow.com/q/75224235/773113
		// 	return source_file_name;
		string solution_source_path = SolutionSourcePath.Value;
		if( !source_file_name.StartsWith( solution_source_path, Sys.StringComparison.Ordinal ) )
			return source_file_name;
		int start = solution_source_path.Length;
		while( start < source_file_name.Length && (source_file_name[start] == '\\' || source_file_name[start] == '/') )
			start++;
		return source_file_name[start..];
	}
}
