namespace VsDebugLoggerKit;

using Sys = System;
using SysComp = System.Runtime.CompilerServices;
using SysDiag = System.Diagnostics;
using static Statics;

public class StatefulLifeGuard : Sys.IDisposable
{
	public static StatefulLifeGuard Create( bool collectStackTrace = false, [SysComp.CallerFilePath] string callerFilePath = null!, //
			[SysComp.CallerLineNumber] int callerLineNumber = 0 )
	{
		return new StatefulLifeGuard( collectStackTrace, 1, callerFilePath, callerLineNumber );
	}

	private readonly LifeGuard lifeGuard;
	public bool IsAlive { get; private set; } = true;

	private StatefulLifeGuard( bool collectStackTrace, int framesToSkip, string callerFilePath, int callerLineNumber = 0 )
	{
		lifeGuard = LifeGuard.Create( framesToSkip + 1, collectStackTrace, callerFilePath, callerLineNumber );
	}

	public void Dispose()
	{
		lifeGuard.Dispose();
		IsAlive = false;
	}

	[SysDiag.DebuggerHidden]
	public bool IsAliveAssertion()
	{
		Assert( IsAlive );
		return true;
	}

	public override string ToString() => lifeGuard.ToString();
}
