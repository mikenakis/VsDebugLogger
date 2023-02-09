namespace VsDebugLogger;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.OLE.Interop;
using VsDebugLogger.Framework;
using VsDebugLogger.Framework.FileSystem;
using VsAutomation80 = EnvDTE80;
using VsAutomation = EnvDTE;
using static Statics;

// From https://www.codeproject.com/Articles/5747/Reading-Command-line-build-logs-into-the-Visual-St converted using https://converter.telerik.com/
public class ResilientVsDebugProxy
{
	private const string output_window_pane_name = "Debug";
	private readonly string solution_name;
	private readonly Procedure<string> logger;
	private VsAutomation.OutputWindowPane? pane;

	public ResilientVsDebugProxy( string solution_name, Procedure<string> logger )
	{
		this.solution_name = solution_name;
		this.logger = logger;
	}

	public bool Write( string text )
	{
		if( pane == null )
		{
			pane = try_get_output_window_pane( solution_name, logger );
			if( pane == null )
				return false;
		}

		try
		{
			pane.OutputString( text );
		}
		catch( Exception exception )
		{
			logger.Invoke( $"Failed to output text to the '{output_window_pane_name}' pane of the Visual Studio Output Window: {exception.GetType()}: {exception.Message}" );
			pane = null;
			return false;
		}
		return true;
	}

	private static VsAutomation.OutputWindowPane? try_get_output_window_pane( string solution_name, Procedure<string> logger )
	{
		try
		{
			return get_vs_output_window_pane( solution_name, logger );
		}
		catch( Exception exception )
		{
			logger.Invoke( $"Failed to acquire the '{output_window_pane_name}' pane of the Visual Studio Output Window: {exception.GetType()}: {exception.Message}" );
			return null;
		}
	}

	private static VsAutomation.DTE? get_vs_instance( string solution_name, Procedure<string> logger )
	{
		List<VsAutomation80.DTE2> vs_instances = enumerate_vs_instances().ToList();
		if( vs_instances.Count == 0 )
		{
			logger.Invoke( "Could not find any running instances of Visual Studio." );
			return null;
		}
		if( solution_name == "" )
			return vs_instances[0];
		foreach( VsAutomation80.DTE2 instance in vs_instances )
		{
			string this_solution_name = get_solution_name_from_vs_instance( instance );
			if( this_solution_name == solution_name )
				return instance; //.DTE;
		}
		logger.Invoke( $"No running instance of Visual Studio has solution '{solution_name}' open." );
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

	private static VsAutomation.OutputWindowPane? get_vs_output_window_pane( string solution_name, Procedure<string> logger )
	{
		VsAutomation.DTE? vs_instance = get_vs_instance( solution_name, logger );
		if( vs_instance == null )
			return null;
		// //PEARL: System.Runtime.InteropServices.Marshal.GetActiveObject() does not exist in DotNetCore. For this reason, we are using Marshal2.
		// VsAutomation.DTE visual_studio_dte = (VsAutomation.DTE)Marshal2.GetActiveObject( "VisualStudio.DTE" );
		VsAutomation.Window window_item = vs_instance.Windows.Item( VsAutomation.Constants.vsWindowKindOutput );
		VsAutomation.OutputWindow output_window = (VsAutomation.OutputWindow)window_item.Object;
		foreach( VsAutomation.OutputWindowPane output_window_pane in output_window.OutputWindowPanes )
			if( output_window_pane.Name == output_window_pane_name )
				return output_window_pane;
		return output_window.OutputWindowPanes.Add( output_window_pane_name );
	}

	[DllImport( "ole32.dll" )]
	private static extern int CreateBindCtx( int reserved, out IBindCtx ppbc );

	[DllImport( "ole32.dll" )]
	private static extern int GetRunningObjectTable( int reserved, out IRunningObjectTable pprot );

	private static IEnumerable<VsAutomation80.DTE2> enumerate_vs_instances()
	{
		int ret_val = GetRunningObjectTable( 0, out IRunningObjectTable rot );
		Assert( ret_val == 0 );
		rot.EnumRunning( out IEnumMoniker enum_moniker );
		IMoniker[] moniker = new IMoniker[1];
		while( enum_moniker.Next( 1, moniker, out uint fetched ) == 0 )
		{
			Assert( fetched == 1 );
			int result = CreateBindCtx( 0, out IBindCtx bind_ctx );
			Assert( result == 0 );
			moniker[0].GetDisplayName( bind_ctx, default, out string display_name );
			if( display_name.StartsWith( "!VisualStudio", StringComparison.Ordinal ) )
			{
				rot.GetObject( moniker[0], out object obj );
				yield return (VsAutomation80.DTE2)obj;
			}
		}
	}
}
