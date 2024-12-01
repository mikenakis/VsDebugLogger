namespace VsDebugLoggerKit;

using Sys = System;
using static Statics;

/// An exception to throw when an assertion fails.
public class AssertionFailureException : Sys.Exception
{
	public readonly object? Expression;

	/// Constructor
	public AssertionFailureException( object? expression ) => Expression = expression;

	/// Constructor
	public AssertionFailureException( object? expression, Sys.Exception? cause )
			: base( null, cause )
		=> Expression = expression;

	public override string Message
	{
		get
		{
			if( Expression == null )
				return string.Empty;
			return NotNull( Expression.ToString() );
		}
	}
}
