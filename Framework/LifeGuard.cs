#nullable enable
namespace Framework
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Threading;
	using Framework.Logging;
	using Sys = Sys;
	using SysDiag = SysDiag;
	using SysComp = System.Runtime.CompilerServices;
	using static Statics;

	// NOTE: a better name for this class would be "ObjectLifeTimeGuard", but that would be too damn long, hence, "LifeGuard".
	public abstract class LifeGuard : Sys.IDisposable
	{
		public static LifeGuard Create( [SysComp.CallerFilePath] string callerFilePath = null!, [SysComp.CallerLineNumber] int callerLineNumber = 0 )
		{
			return Create( framesToSkip: 1, false, callerFilePath, callerLineNumber );
		}

		public static LifeGuard Create( bool collectStackTrace, [SysComp.CallerFilePath] string callerFilePath = null!, [SysComp.CallerLineNumber] int callerLineNumber = 0 )
		{
			return Create( framesToSkip: 1, collectStackTrace, callerFilePath, callerLineNumber );
		}

		public static LifeGuard Create( int framesToSkip, bool collectStackTrace = false, [SysComp.CallerFilePath] string callerFilePath = null!, [SysComp.CallerLineNumber] int callerLineNumber = 0 )
		{
			Assert( callerFilePath != null );
			if( !DebugMode )
				return ProductionLifeGuard.Instance;
			if( collectStackTrace )
				return new VerboseDebugLifeGuard( callerFilePath, callerLineNumber, framesToSkip + 1 );
			return new TerseDebugLifeGuard( callerFilePath, callerLineNumber );
		}

		public abstract void Dispose();

		public abstract bool IsAliveAssertion();

		public abstract override string ToString();

		private sealed class ProductionLifeGuard : LifeGuard
		{
			public static readonly ProductionLifeGuard Instance = new();

			private ProductionLifeGuard()
			{ } //nothing to do

			public override void Dispose()
			{ } //nothing to do

			public override bool IsAliveAssertion() => throw new Sys.Exception(); //never invoke on a release build

			public override string ToString() => "";
		}

		private abstract class DebugLifeGuard : LifeGuard
		{
			private static long idSeed;

			private bool alive = true;
			private readonly string callerFilePath;
			private readonly int callerLineNumber;
			private readonly string message;
			private readonly long objectId = Interlocked.Increment( ref idSeed );

			protected DebugLifeGuard( string callerFilePath, int callerLineNumber, string message )
			{
				this.callerFilePath = callerFilePath;
				this.callerLineNumber = callerLineNumber;
				this.message = message;
			}

			public sealed override void Dispose()
			{
				Assert( alive );
				alive = false;
				Sys.GC.SuppressFinalize( this );
			}

			[SysDiag.DebuggerHidden]
			public sealed override bool IsAliveAssertion()
			{
				Assert( alive );
				return true;
			}

			protected static string GetSourceInfo( string? filename, int lineNumber ) => $"{filename}({lineNumber})";

			~DebugLifeGuard()
			{
				report( objectId, message, callerFilePath, callerLineNumber );
			}

			public override string ToString() => $"objectId=0x{objectId:x}{(alive ? "" : " END-OF-LIFE")}";

			private readonly struct SourceLocation
			{
				private readonly string filePath;
				private readonly int lineNumber;

				public SourceLocation( string filePath, int lineNumber )
				{
					this.filePath = filePath;
					this.lineNumber = lineNumber;
				}

				[Sys.Obsolete] public override bool Equals( object? other ) => other is SourceLocation kin && equals( kin );

				private bool equals( SourceLocation other ) => filePath == other.filePath && lineNumber == other.lineNumber;
				public override int GetHashCode() => Sys.HashCode.Combine( filePath, lineNumber );
			}

			private static readonly ICollection<SourceLocation> reportedSourceLocations = new HashSet<SourceLocation>();

			private static void report( long objectId, string message, string callerFilePath, int callerLineNumber )
			{
				SourceLocation callerSourceLocation = new SourceLocation( callerFilePath, callerLineNumber );
				lock( reportedSourceLocations )
				{
					if( reportedSourceLocations.Contains( callerSourceLocation ) )
						return;
					reportedSourceLocations.Add( callerSourceLocation );
				}
				Log.LogRawMessage( LogLevel.Error, $"IDisposable allocated at this source location was never disposed! id=0x{objectId:x}. {message}", callerFilePath, callerLineNumber );
				Breakpoint(); //you may resume program execution to see more leaked disposables, but please fix this before committing.
			}
		}

		private sealed class TerseDebugLifeGuard : DebugLifeGuard
		{
			public TerseDebugLifeGuard( string callerFilePath, int callerLineNumber )
					: base( callerFilePath, callerLineNumber, $"To enable stack trace collection for this class, pass 'true' to the {nameof(LifeGuard)}.{nameof(Create)}() method call." )
			{ }
		}

		private sealed class VerboseDebugLifeGuard : DebugLifeGuard
		{
			public VerboseDebugLifeGuard( string callerFilePath, int callerLineNumber, int framesToSkip )
					: base( callerFilePath, callerLineNumber, build_message( framesToSkip + 1 ) )
			{ }

			private static string build_message( int framesToSkip )
			{
				IEnumerable<string> sourceInfos = get_stack_frames( framesToSkip + 1 ).Select( get_source_info_from_stack_frame );
				return "Stack Trace:\r\n" + string.Join( "\r\n", sourceInfos );
			}

			private static IEnumerable<SysDiag.StackFrame> get_stack_frames( int framesToSkip )
			{
				var stackTrace = new SysDiag.StackTrace( framesToSkip + 1, true );
				SysDiag.StackFrame[] frames = stackTrace.GetFrames();
				MethodBase? method = frames[0].GetMethod();
				Assert( method == null || method.DeclaringType == null || typeof(Sys.IDisposable).IsAssignableFrom( method.DeclaringType ) );
				return frames.Where( f => f.GetFileName() != null );
			}

			private static string get_source_info_from_stack_frame( SysDiag.StackFrame frame )
			{
				string sourceInfo = GetSourceInfo( frame.GetFileName(), frame.GetFileLineNumber() );
				MethodBase? method = frame.GetMethod();
				Sys.Type? declaringType = method?.DeclaringType;
				return $"    {sourceInfo}: {declaringType?.FullName ?? "?"}.{method?.Name ?? "?"}()";
			}
		}
	}
}
