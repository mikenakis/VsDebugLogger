namespace Framework.Logging;

using SysDiag = global::System.Diagnostics;

//PEARL: Excessive use of this class slows down debug runs, because when the Visual Studio Debugger is active, it
//       intercepts DotNet debug output and does extremely time-consuming stuff with it. For a full explanation, and
//       also for an explanation as to why this cannot be avoided, see https://stackoverflow.com/a/72894816/773113
public class DebugLogger
{
	public static readonly Logger Instance = new FormattingLogger( write ).EntryPoint;

	private static void write( string text ) => SysDiag.Debug.WriteLine( text );
}
