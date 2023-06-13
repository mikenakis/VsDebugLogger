namespace Framework;

using Sys = System;
using SysComp = System.Runtime.CompilerServices;

public static class SolutionSourcePath
{
	private const string myRelativePath = "VsDebugLogger\\Framework\\" + nameof(SolutionSourcePath) + ".cs";
	private static string? lazyValue;
	public static string Value => lazyValue ??= calculate_solution_source_path();

	// NOTE: this function is invoked from the logging subsystem, so it must refrain from causing anything to be logged,
	//       otherwise there will be a StackOverflowException.
	private static string calculate_solution_source_path()
	{
		string sourceFileName = get_source_file_name();
		if( !sourceFileName.EndsWith( myRelativePath, Sys.StringComparison.Ordinal ) )
			throw new Sys.Exception( sourceFileName );
		return sourceFileName[..^myRelativePath.Length];
	}

	private static string get_source_file_name( [SysComp.CallerFilePath] string? sourceFileName = null )
	{
		if( sourceFileName == null )
			throw new Sys.Exception( sourceFileName );
		return sourceFileName;
	}
}
