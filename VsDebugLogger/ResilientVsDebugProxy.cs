namespace VsDebugLogger;

using System;
using System.Collections.Generic;
using System.Linq;
using Framework.FileSystem;
using VsInterop = Microsoft.VisualStudio.OLE.Interop;
using VsAutomation80 = EnvDTE80;
using VsAutomation = EnvDTE;
using static Framework.Statics;

// Portions from https://www.codeproject.com/Articles/5747/Reading-Command-line-build-logs-into-the-Visual-St converted using https://converter.telerik.com/
// Portions from Stack Overflow: "How do I get the DTE for running Visual Studio instance?" https://stackoverflow.com/a/14205934/773113
public class ResilientVsDebugProxy
{
	// ReSharper disable InconsistentNaming
	private const int RPC_E_CALL_REJECTED = unchecked((int)0x80010001U);
	// ReSharper restore InconsistentNaming

	private const string output_window_pane_name = "Debug";
	private readonly string solution_name;
	private VsAutomation.OutputWindowPane? pane;

	public ResilientVsDebugProxy( string solution_name )
	{
		this.solution_name = solution_name;
	}

	public bool Write( string text )
	{
		return try_output_text_to_pane( solution_name, ref pane, text );
	}

	private static bool try_output_text_to_pane( string solution_name, ref VsAutomation.OutputWindowPane? pane, string text )
	{
		if( pane == null )
		{
			pane = try_get_output_window_pane( solution_name );
			if( pane == null )
				return false;
		}

		try
		{
			pane.OutputString( text );
		}
		catch( Exception exception )
		{
			string message = $"Failed to output text to '{solution_name}'";
			if( exception is SysInterop.COMException com_exception && com_exception.HResult == RPC_E_CALL_REJECTED )
				Log.Warn( $"{message}. Reason: \"Call was rejected\". (Typical Microsoft nonsense.)" );
			else
				Log.Error( $"{message}: {exception.GetType()}: {exception.Message}" );
			pane = null;
			return false;
		}
		return true;
	}

	private static VsAutomation.OutputWindowPane? try_get_output_window_pane( string solution_name )
	{
		try
		{
			return get_vs_output_window_pane( solution_name );
		}
		catch( Exception exception )
		{
			string message = $"Failed to acquire the '{output_window_pane_name}' pane of '{solution_name}'";
			if( exception is SysInterop.COMException com_exception && com_exception.HResult == RPC_E_CALL_REJECTED )
				Log.Warn( $"{message}: Reason: \"Call was rejected\". (Typical Microsoft nonsense.)" );
			else
				Log.Error( $"{message}: {exception.GetType()}: {exception.Message}" );
			return null;
		}
	}

	private static VsAutomation.DTE? get_vs_instance( string solution_name )
	{
		List<VsAutomation80.DTE2> vs_instances = enumerate_vs_instances().ToList();
		if( vs_instances.Count == 0 )
		{
			Log.Error( "Could not find any running instances of Visual Studio." );
			return null;
		}
		if( solution_name == "" )
			return vs_instances[0];
		foreach( VsAutomation80.DTE2 vs_instance in vs_instances )
		{
			string this_solution_name = get_solution_name_from_vs_instance( vs_instance );
			if( this_solution_name == solution_name )
				return vs_instance;
		}
		Log.Error( $"No running instance of Visual Studio has solution '{solution_name}' open." );
		return null;
	}

	private static string get_solution_name_from_vs_instance( VsAutomation80.DTE2 vs_instance )
	{
		if( False )
		{
			//PEARL: The 'EnvDTE.Solution.FullName' property does not contain a "full name", it actually contains the full pathname to the solution.
			//Check out Microsoft's despicable corp-talk about this here:
			//Visual Studio Developer Community - Question: "DTE2 Solution.Fullname return a filepath" - Resolution: "Closed - Not a Bug"
			//https://developercommunity.visualstudio.com/t/dte2-solutionfullname-return-a-filepath/43005
			FilePath solution_file_path = FilePath.FromAbsolutePath( vs_instance.Solution.FullName );
			return solution_file_path.GetFileNameWithoutExtension();
		}
		else
		{
			return (string)vs_instance.Solution.Properties.Item( "Name" ).Value;
		}
	}

	private static VsAutomation.OutputWindowPane? get_vs_output_window_pane( string solution_name )
	{
		VsAutomation.DTE? vs_instance = get_vs_instance( solution_name );
		if( vs_instance == null )
			return null;
		VsAutomation.Window window_item = vs_instance.Windows.Item( VsAutomation.Constants.vsWindowKindOutput );
		VsAutomation.OutputWindow output_window = (VsAutomation.OutputWindow)window_item.Object;
		foreach( VsAutomation.OutputWindowPane output_window_pane in output_window.OutputWindowPanes )
			if( output_window_pane.Name == output_window_pane_name )
				return output_window_pane;
		return output_window.OutputWindowPanes.Add( output_window_pane_name );
	}

	[SysInterop.DllImport( "ole32.dll" )] private static extern int CreateBindCtx( int reserved, out VsInterop.IBindCtx bind_ctx );

	[SysInterop.DllImport( "ole32.dll" )] private static extern int GetRunningObjectTable( int reserved, out VsInterop.IRunningObjectTable running_object_table );

	private static IEnumerable<VsAutomation80.DTE2> enumerate_vs_instances()
	{
		int result = GetRunningObjectTable( 0, out VsInterop.IRunningObjectTable running_object_table );
		Assert( result == 0 );
		running_object_table.EnumRunning( out VsInterop.IEnumMoniker enum_moniker );
		VsInterop.IMoniker[] moniker = new VsInterop.IMoniker[1];
		while( enum_moniker.Next( 1, moniker, out uint fetched ) == 0 )
		{
			Assert( fetched == 1 );
			result = CreateBindCtx( 0, out VsInterop.IBindCtx bind_ctx );
			Assert( result == 0 );
			moniker[0].GetDisplayName( bind_ctx, default, out string display_name );
			if( display_name.StartsWith( "!VisualStudio", StringComparison.Ordinal ) )
			{
				running_object_table.GetObject( moniker[0], out object obj );
				yield return (VsAutomation80.DTE2)obj;
			}
		}
	}
}
