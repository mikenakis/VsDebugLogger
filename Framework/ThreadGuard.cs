namespace Framework;

using Sys = System;
using SysThread = System.Threading;
using static Statics;

public abstract class ThreadGuard
{
	public static ThreadGuard Create()
	{
		if( DebugMode )
			return new DebugThreadGuard();
		return ProductionThreadGuard.Instance;
	}

	public abstract bool InThreadAssertion();

	public abstract bool OutOfThreadAssertion();

	private sealed class ProductionThreadGuard : ThreadGuard
	{
		public static readonly ProductionThreadGuard Instance = new();

		private ProductionThreadGuard()
		{ }

		public override bool InThreadAssertion() => throw new Sys.Exception(); //never invoke on a release build

		public override bool OutOfThreadAssertion() => throw new Sys.Exception(); //never invoke on a release build
	}

	private sealed class DebugThreadGuard : ThreadGuard
	{
		private readonly SysThread.Thread thread;

		public DebugThreadGuard()
		{
			thread = SysThread.Thread.CurrentThread;
		}

		public override bool InThreadAssertion() => ReferenceEquals( SysThread.Thread.CurrentThread, thread );

		public override bool OutOfThreadAssertion() => !ReferenceEquals( SysThread.Thread.CurrentThread, thread );

		public override string ToString() => $"thread={thread}";
	}
}
