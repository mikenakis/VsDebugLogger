namespace Framework;

using Sys = Sys;
using SysDiag = SysDiag;
using SysCompiler = SysCompiler;
using SysThread = SysThread;

public static class Statics
{
	///<summary>Always returns `true`.</summary>
	///<remarks>Allows `if( True )` without a "condition is always true" warning.</remarks>
	///<remarks>Allows code to be enabled/disabled while still having to pass compilation, thus preventing code rot.</remarks>
	public static bool True => true;

	///<summary>Always returns `false`.</summary>
	///<remarks>Allows `if( False )` without a "condition is always false" warning.</remarks>
	///<remarks>Allows code to be enabled/disabled while still having to pass compilation, thus preventing code rot.</remarks>
	public static bool False => false;

	///<summary>Returns `true` if `DEBUG` has been defined.</summary>
	///<remarks>Allows code to be enabled/disabled while still having to pass compilation, thus preventing code rot.</remarks>
	public static bool DebugMode
	{
		get
		{
#if DEBUG
			return true;
#else
				return false;
#endif
		}
	}

	public static bool Debugging => DebugMode && SysDiag.Debugger.IsAttached;

	///<summary>Identity function.</summary>
	///<remarks>useful as a no-op lambda and sometimes as a debugging aid.</remarks>
	public static T Get<T>( T value ) => value;

	[SysDiag.DebuggerHidden, SysDiag.Conditional( "DEBUG" )]
	public static void Assert( [SysCodeAnalysis.DoesNotReturnIf( false )] bool condition, //
			[SysCompiler.CallerFilePath]
			string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 )
		=> Assert( condition, null, null, source_file_name, source_line_number );

	[SysDiag.DebuggerHidden, SysDiag.Conditional( "DEBUG" )]
	public static void Assert( [SysCodeAnalysis.DoesNotReturnIf( false )] bool condition, Function<Sys.Exception> exception_factory, [SysCompiler.CallerFilePath] string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 ) => Assert( condition, null, exception_factory, source_file_name, source_line_number );

	/// <summary>Checks the supplied 'condition' boolean and throws an <see cref="Sys.Exception" /> if <c>false</c></summary>
	[SysDiag.DebuggerHidden]
	public static void Assert( bool condition, object? expression, Function<Sys.Exception>? converter, [SysCompiler.CallerFilePath] string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 )
	{
		if( condition )
			return;
		string message = "" + expression;
		if( !FailureTesting.Value )
		{
			Log.Error( $"Assertion failed{(message == "" ? "" : ": " + message)}", source_file_name, source_line_number );
			if( Breakpoint() )
				return;
		}
		Sys.Exception? cause = null;
		if( converter != null )
			cause = converter.Invoke();
		throw new AssertionFailureException( message, cause );
	}

	[SysDiag.DebuggerHidden]
	public static T NotNull<T>( T? pointer, //
			[SysCompiler.CallerFilePath]
			string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 ) //
			where T : class
		=> NotNull0( pointer, source_file_name, source_line_number );

	[SysDiag.DebuggerHidden]
	public static T NotNull<T>( T? nullable_value, //
			[SysCompiler.CallerFilePath]
			string source_file_name = "", [SysCompiler.CallerLineNumber] int source_line_number = 0 ) //
			where T : struct
		=> NotNull0( nullable_value, source_file_name, source_line_number );

	/// <summary>Returns the supplied pointer unchanged, while asserting that it is non-null.</summary>
	[SysDiag.DebuggerHidden]
	public static T NotNull0<T>( T? pointer, string source_file_name, int source_line_number ) where T : class
	{
		Assert( pointer != null, source_file_name, source_line_number );
		return pointer;
	}

	/// <summary>Converts a nullable value to non-nullable, while asserting that it is non-null.</summary>
	[SysDiag.DebuggerHidden]
	public static T NotNull0<T>( T? nullable_value, string source_file_name, int source_line_number ) where T : struct
	{
		Assert( nullable_value.HasValue, source_file_name, source_line_number );
		return nullable_value.Value;
	}

	/// <summary>If a debugger is attached, hits a breakpoint and returns <c>true</c>; otherwise, returns <c>false</c></summary>
	[SysDiag.DebuggerHidden]
	public static bool Breakpoint()
	{
		if( SysDiag.Debugger.IsAttached )
		{
			SysDiag.Debugger.Break(); //Note: this is problematic due to some Visual Studio bug: when it hits, you are prevented from setting the next statement either within the calling function or within this function.
			return true;
		}
		return false;
	}

	public static Sys.Exception NewInvalidArgumentException<T>( string argument_name, T argument_value, string? additional_message = null )
	{
		var type_string = FrameworkHelpers.GetCSharpTypeName( typeof(T) );
		var value_string = FrameworkHelpers.SafeToString( argument_value );
		string message = $"Invalid argument '{argument_name}': type={type_string} value={value_string} {additional_message}";
		var result = new Sys.Exception( message );
		Assert( false, message );
		return result;
	}

	public static readonly SysThread.ThreadLocal<bool> FailureTesting = new( false );

	public const double Epsilon = 1e-15;

	///<summary>Compares two `double` values.</summary>
	//TODO: perhaps replace with something more sophisticated, like this: https://stackoverflow.com/a/3875619/773113
	public static bool DoubleEquals( double a, double b, double? maybe_tolerance = null )
	{
		if( double.IsNaN( a ) && double.IsNaN( b ) )
			return true;
		double difference = Math.Abs( a - b );
		double tolerance = maybe_tolerance ?? Epsilon;
		return difference < tolerance;
	}

	///<summary>Compares two <code>double</code> values for exact equality, avoiding the "equality comparison of floating point numbers" inspection.</summary>
	public static bool DoubleExactlyEquals( double a, double b )
	{
		return a.Equals( b );
	}

	public const float FEpsilon = 1.192093E-07f;

	///<summary>Compares two `double` values, using a specific tolerance.</summary>
	//TODO: perhaps replace with something more sophisticated, like this: https://stackoverflow.com/a/3875619/773113
	public static bool FloatEquals( float a, float b, float? maybe_tolerance = null )
	{
		if( float.IsNaN( a ) && float.IsNaN( b ) )
			return true;
		float difference = Math.Abs( a - b );
		float tolerance = maybe_tolerance ?? FEpsilon;
		return difference < tolerance;
	}

	///<summary>Compares two <code>float</code> values for exact equality, avoiding the "equality comparison of floating point numbers" inspection.</summary>
	public static bool FloatExactlyEquals( float a, float b )
	{
		return a.Equals( b );
	}
}
