namespace VsDebugLogger;

using Sys = System;
using SysComp = System.Runtime.CompilerServices;

public static class SolutionSourcePath
{
	private const string my_relative_path = "Framework\\" + nameof(SolutionSourcePath) + ".cs";
	private static string? lazy_value;
	public static string Value => lazy_value ??= calculate_solution_source_path();

	// NOTE: this function is invoked from the logging subsystem, so it must refrain from causing anything to be logged,
	//       otherwise there will be a StackOverflowException.
	private static string calculate_solution_source_path()
	{
		string source_file_name = get_source_file_name();
		if( !source_file_name.EndsWith( my_relative_path, Sys.StringComparison.Ordinal ) )
			throw new Sys.Exception( source_file_name );
		return source_file_name[..^my_relative_path.Length];
	}

	private static string get_source_file_name( [SysComp.CallerFilePath] string? source_file_name = null )
	{
		if( source_file_name == null )
			throw new Sys.Exception( source_file_name );
		return source_file_name;
	}
}
