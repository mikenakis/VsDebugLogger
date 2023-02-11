namespace Framework;

using Sys = Sys;
using SysComp = System.Runtime.CompilerServices;
using SysDiag = SysDiag;
using static Statics;

public class StatefulLifeGuard : Sys.IDisposable
{
	public static StatefulLifeGuard Create( bool collect_stack_trace = false, [SysComp.CallerFilePath] string caller_file_path = null!, //
			[SysComp.CallerLineNumber] int caller_line_number = 0 )
	{
		return new StatefulLifeGuard( collect_stack_trace, 1, caller_file_path, caller_line_number );
	}

	private readonly LifeGuard life_guard;
	public bool IsAlive { get; private set; } = true;

	private StatefulLifeGuard( bool collect_stack_trace, int frames_to_skip, string caller_file_path, int caller_line_number = 0 )
	{
		life_guard = LifeGuard.Create( frames_to_skip + 1, collect_stack_trace, caller_file_path, caller_line_number );
	}

	public void Dispose()
	{
		life_guard.Dispose();
		IsAlive = false;
	}

	[SysDiag.DebuggerHidden]
	public bool IsAliveAssertion()
	{
		Assert( IsAlive );
		return true;
	}

	public override string ToString() => life_guard.ToString();
}
