namespace VsDebugLoggerKit;

using Log = Logging.Log;
using Math = System.Math;
using Sys = System;
using SysCodeAnalysis = System.Diagnostics.CodeAnalysis;
using SysCompiler = System.Runtime.CompilerServices;
using SysDiag = System.Diagnostics;
using SysThread = System.Threading;

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
	public static void Assert( [SysCodeAnalysis.DoesNotReturnIf( false )] bool condition, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) //
		=> Assert( condition, null, null, sourceFileName, sourceLineNumber );

	[SysDiag.DebuggerHidden, SysDiag.Conditional( "DEBUG" )]
	public static void Assert( [SysCodeAnalysis.DoesNotReturnIf( false )] bool condition, Function<Sys.Exception> exceptionFactory, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) //
		=> Assert( condition, null, exceptionFactory, sourceFileName, sourceLineNumber );

	/// <summary>Checks the supplied 'condition' boolean and throws an <see cref="Sys.Exception" /> if <c>false</c></summary>
	[SysDiag.DebuggerHidden]
	public static void Assert( bool condition, object? expression, Function<Sys.Exception>? converter, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 )
	{
		if( condition )
			return;
		string message = "" + expression;
		if( !FailureTesting.Value )
		{
			Log.Error( $"Assertion failed{(message == "" ? "" : ": " + message)}", sourceFileName, sourceLineNumber );
			if( Breakpoint() )
				return;
		}
		Sys.Exception? cause = null;
		if( converter != null )
			cause = converter.Invoke();
		throw new AssertionFailureException( message, cause );
	}

	[SysDiag.DebuggerHidden]
	public static T NotNull<T>( T? pointer, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) where T : class //
		=> NotNull0( pointer, sourceFileName, sourceLineNumber );

	[SysDiag.DebuggerHidden]
	public static T NotNull<T>( T? nullableValue, [SysCompiler.CallerFilePath] string sourceFileName = "", [SysCompiler.CallerLineNumber] int sourceLineNumber = 0 ) where T : struct //
		=> NotNull0( nullableValue, sourceFileName, sourceLineNumber );

	/// <summary>Returns the supplied pointer unchanged, while asserting that it is non-null.</summary>
	[SysDiag.DebuggerHidden]
	public static T NotNull0<T>( T? pointer, string sourceFileName, int sourceLineNumber ) where T : class
	{
		Assert( pointer != null, sourceFileName, sourceLineNumber );
		return pointer;
	}

	/// <summary>Converts a nullable value to non-nullable, while asserting that it is non-null.</summary>
	[SysDiag.DebuggerHidden]
	public static T NotNull0<T>( T? nullableValue, string sourceFileName, int sourceLineNumber ) where T : struct
	{
		Assert( nullableValue.HasValue, sourceFileName, sourceLineNumber );
		return nullableValue.Value;
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

	public static Sys.Exception NewInvalidArgumentException<T>( string argumentName, T argumentValue, string? additionalMessage = null )
	{
		string typeString = FrameworkHelpers.GetCSharpTypeName( typeof( T ) );
		string valueString = FrameworkHelpers.SafeToString( argumentValue );
		string message = $"Invalid argument '{argumentName}': type={typeString} value={valueString} {additionalMessage}";
		var result = new Sys.Exception( message );
		Assert( false, message );
		return result;
	}

	public static readonly SysThread.ThreadLocal<bool> FailureTesting = new( false );

	public const double Epsilon = 1e-15;

	///<summary>Compares two `double` values.</summary>
	//TODO: perhaps replace with something more sophisticated, like this: https://stackoverflow.com/a/3875619/773113
	public static bool DoubleEquals( double a, double b, double? maybeTolerance = null )
	{
		if( double.IsNaN( a ) && double.IsNaN( b ) )
			return true;
		double difference = Math.Abs( a - b );
		double tolerance = maybeTolerance ?? Epsilon;
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
	public static bool FloatEquals( float a, float b, float? maybeTolerance = null )
	{
		if( float.IsNaN( a ) && float.IsNaN( b ) )
			return true;
		float difference = Math.Abs( a - b );
		float tolerance = maybeTolerance ?? FEpsilon;
		return difference < tolerance;
	}

	///<summary>Compares two <code>float</code> values for exact equality, avoiding the "equality comparison of floating point numbers" inspection.</summary>
	public static bool FloatExactlyEquals( float a, float b )
	{
		return a.Equals( b );
	}
}
