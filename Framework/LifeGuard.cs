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
		public static LifeGuard Create( [SysComp.CallerFilePath] string caller_file_path = null!, [SysComp.CallerLineNumber] int caller_line_number = 0 )
		{
			return Create( frames_to_skip: 1, false, caller_file_path, caller_line_number );
		}

		public static LifeGuard Create( bool collect_stack_trace, [SysComp.CallerFilePath] string caller_file_path = null!, [SysComp.CallerLineNumber] int caller_line_number = 0 )
		{
			return Create( frames_to_skip: 1, collect_stack_trace, caller_file_path, caller_line_number );
		}

		public static LifeGuard Create( int frames_to_skip, bool collect_stack_trace = false, [SysComp.CallerFilePath] string caller_file_path = null!, [SysComp.CallerLineNumber] int caller_line_number = 0 )
		{
			Assert( caller_file_path != null );
			if( !DebugMode )
				return ProductionLifeGuard.Instance;
			if( collect_stack_trace )
				return new VerboseDebugLifeGuard( caller_file_path, caller_line_number, frames_to_skip + 1 );
			return new TerseDebugLifeGuard( caller_file_path, caller_line_number );
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
			private static long id_seed;

			private bool alive = true;
			private readonly string caller_file_path;
			private readonly int caller_line_number;
			private readonly string message;
			private readonly long object_id = Interlocked.Increment( ref id_seed );

			protected DebugLifeGuard( string caller_file_path, int caller_line_number, string message )
			{
				this.caller_file_path = caller_file_path;
				this.caller_line_number = caller_line_number;
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

			protected static string GetSourceInfo( string? filename, int line_number ) => $"{filename}({line_number})";

			~DebugLifeGuard()
			{
				report( object_id, message, caller_file_path, caller_line_number );
			}

			public override string ToString() => $"objectId=0x{object_id:x}{(alive ? "" : " END-OF-LIFE")}";

			private readonly struct SourceLocation
			{
				private readonly string file_path;
				private readonly int line_number;

				public SourceLocation( string file_path, int line_number )
				{
					this.file_path = file_path;
					this.line_number = line_number;
				}

				[Sys.Obsolete] public override bool Equals( object? other ) => other is SourceLocation kin && equals( kin );

				private bool equals( SourceLocation other ) => file_path == other.file_path && line_number == other.line_number;
				public override int GetHashCode() => Sys.HashCode.Combine( file_path, line_number );
			}

			private static readonly ICollection<SourceLocation> reported_source_locations = new HashSet<SourceLocation>();

			private static void report( long object_id, string message, string caller_file_path, int caller_line_number )
			{
				SourceLocation caller_source_location = new SourceLocation( caller_file_path, caller_line_number );
				lock( reported_source_locations )
				{
					if( reported_source_locations.Contains( caller_source_location ) )
						return;
					reported_source_locations.Add( caller_source_location );
				}
				Log.LogRawMessage( LogLevel.Error, $"IDisposable allocated at this source location was never disposed! id=0x{object_id:x}. {message}", caller_file_path, caller_line_number );
				Breakpoint(); //you may resume program execution to see more leaked disposables, but please fix this before committing.
			}
		}

		private sealed class TerseDebugLifeGuard : DebugLifeGuard
		{
			public TerseDebugLifeGuard( string caller_file_path, int caller_line_number )
					: base( caller_file_path, caller_line_number, $"To enable stack trace collection for this class, pass 'true' to the {nameof(LifeGuard)}.{nameof(Create)}() method call." )
			{ }
		}

		private sealed class VerboseDebugLifeGuard : DebugLifeGuard
		{
			public VerboseDebugLifeGuard( string caller_file_path, int caller_line_number, int frames_to_skip )
					: base( caller_file_path, caller_line_number, build_message( frames_to_skip + 1 ) )
			{ }

			private static string build_message( int frames_to_skip )
			{
				IEnumerable<string> source_infos = get_stack_frames( frames_to_skip + 1 ).Select( get_source_info_from_stack_frame );
				return "Stack Trace:\r\n" + string.Join( "\r\n", source_infos );
			}

			private static IEnumerable<SysDiag.StackFrame> get_stack_frames( int frames_to_skip )
			{
				var stack_trace = new SysDiag.StackTrace( frames_to_skip + 1, true );
				SysDiag.StackFrame[] frames = stack_trace.GetFrames();
				MethodBase? method = frames[0].GetMethod();
				Assert( method == null || method.DeclaringType == null || typeof(Sys.IDisposable).IsAssignableFrom( method.DeclaringType ) );
				return frames.Where( f => f.GetFileName() != null );
			}

			private static string get_source_info_from_stack_frame( SysDiag.StackFrame frame )
			{
				string source_info = GetSourceInfo( frame.GetFileName(), frame.GetFileLineNumber() );
				MethodBase? method = frame.GetMethod();
				Sys.Type? declaring_type = method?.DeclaringType;
				return $"    {source_info}: {declaring_type?.FullName ?? "?"}.{method?.Name ?? "?"}()";
			}
		}
	}
}
