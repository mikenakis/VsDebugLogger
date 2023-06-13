namespace VsDebugLogger;

using SysInterop = global::System.Runtime.InteropServices;
using global::System.Collections.Generic;
using global::System.Linq;
using static global::Framework.Statics;
using Log = global::Framework.Logging.Log;
using System;
using Framework.FileSystem;
using VsInterop = Microsoft.VisualStudio.OLE.Interop;
using VsAutomation80 = EnvDTE80;
using VsAutomation = EnvDTE;

// Portions from https://www.codeproject.com/Articles/5747/Reading-Command-line-build-logs-into-the-Visual-St converted using https://converter.telerik.com/
// Portions from Stack Overflow: "How do I get the DTE for running Visual Studio instance?" https://stackoverflow.com/a/14205934/773113
public class ResilientVsDebugProxy
{
	// ReSharper disable InconsistentNaming
	private const int RPC_E_CALL_REJECTED = unchecked((int)0x80010001U);
	// ReSharper restore InconsistentNaming

	private const string outputWindowPaneName = "Debug";
	private readonly string solutionName;
	private VsAutomation.OutputWindowPane? pane;

	public ResilientVsDebugProxy( string solutionName )
	{
		this.solutionName = solutionName;
	}

	public bool Write( string text )
	{
		return try_output_text_to_pane( solutionName, ref pane, text );
	}

	private static bool try_output_text_to_pane( string solutionName, ref VsAutomation.OutputWindowPane? pane, string text )
	{
		if( pane == null )
		{
			pane = try_get_output_window_pane( solutionName );
			if( pane == null )
				return false;
		}

		try
		{
			pane.OutputString( text );
		}
		catch( Exception exception )
		{
			string message = $"Failed to output text to '{solutionName}'";
			if( exception is SysInterop.COMException comException && comException.HResult == RPC_E_CALL_REJECTED )
				Log.Warn( $"{message}. Reason: \"Call was rejected\". (Typical Microsoft nonsense.)" );
			else
				Log.Error( $"{message}: {exception.GetType()}: {exception.Message}" );
			pane = null;
			return false;
		}
		return true;
	}

	private static VsAutomation.OutputWindowPane? try_get_output_window_pane( string solutionName )
	{
		try
		{
			return get_vs_output_window_pane( solutionName );
		}
		catch( Exception exception )
		{
			string message = $"Failed to acquire the '{outputWindowPaneName}' pane of '{solutionName}'";
			if( exception is SysInterop.COMException comException && comException.HResult == RPC_E_CALL_REJECTED )
				Log.Warn( $"{message}: Reason: \"Call was rejected\". (Typical Microsoft nonsense.)" );
			else
				Log.Error( $"{message}: {exception.GetType()}: {exception.Message}" );
			return null;
		}
	}

	private static VsAutomation.DTE? get_vs_instance( string solutionName )
	{
		List<VsAutomation80.DTE2> vsInstances = enumerate_vs_instances().ToList();
		if( vsInstances.Count == 0 )
		{
			Log.Error( "Could not find any running instances of Visual Studio." );
			return null;
		}
		if( solutionName == "" )
			return vsInstances[0];
		foreach( VsAutomation80.DTE2 vsInstance in vsInstances )
		{
			string thisSolutionName = get_solution_name_from_vs_instance( vsInstance );
			if( thisSolutionName == solutionName )
				return vsInstance;
		}
		Log.Error( $"No running instance of Visual Studio has solution '{solutionName}' open." );
		return null;
	}

	private static string get_solution_name_from_vs_instance( VsAutomation80.DTE2 vsInstance )
	{
		if( False )
		{
			//PEARL: The 'EnvDTE.Solution.FullName' property does not contain a "full name", it actually contains the full pathname to the solution.
			//Check out Microsoft's despicable corp-talk about this here:
			//Visual Studio Developer Community - Question: "DTE2 Solution.Fullname return a filepath" - Resolution: "Closed - Not a Bug"
			//https://developercommunity.visualstudio.com/t/dte2-solutionfullname-return-a-filepath/43005
			FilePath solutionFilePath = FilePath.FromAbsolutePath( vsInstance.Solution.FullName );
			return solutionFilePath.GetFileNameWithoutExtension();
		}
		else
		{
			return (string)vsInstance.Solution.Properties.Item( "Name" ).Value;
		}
	}

	private static VsAutomation.OutputWindowPane? get_vs_output_window_pane( string solutionName )
	{
		VsAutomation.DTE? vsInstance = get_vs_instance( solutionName );
		if( vsInstance == null )
			return null;
		VsAutomation.Window windowItem = vsInstance.Windows.Item( VsAutomation.Constants.vsWindowKindOutput );
		VsAutomation.OutputWindow outputWindow = (VsAutomation.OutputWindow)windowItem.Object;
		foreach( VsAutomation.OutputWindowPane outputWindowPane in outputWindow.OutputWindowPanes )
			if( outputWindowPane.Name == outputWindowPaneName )
				return outputWindowPane;
		return outputWindow.OutputWindowPanes.Add( outputWindowPaneName );
	}

	[SysInterop.DllImport( "ole32.dll" )] private static extern int CreateBindCtx( int reserved, out VsInterop.IBindCtx bindCtx );

	[SysInterop.DllImport( "ole32.dll" )] private static extern int GetRunningObjectTable( int reserved, out VsInterop.IRunningObjectTable runningObjectTable );

	private static IEnumerable<VsAutomation80.DTE2> enumerate_vs_instances()
	{
		int result = GetRunningObjectTable( 0, out VsInterop.IRunningObjectTable runningObjectTable );
		Assert( result == 0 );
		runningObjectTable.EnumRunning( out VsInterop.IEnumMoniker enumMoniker );
		VsInterop.IMoniker[] moniker = new VsInterop.IMoniker[1];
		while( enumMoniker.Next( 1, moniker, out uint fetched ) == 0 )
		{
			Assert( fetched == 1 );
			result = CreateBindCtx( 0, out VsInterop.IBindCtx bindCtx );
			Assert( result == 0 );
			moniker[0].GetDisplayName( bindCtx, default, out string displayName );
			if( displayName.StartsWith( "!VisualStudio", StringComparison.Ordinal ) )
			{
				runningObjectTable.GetObject( moniker[0], out object obj );
				yield return (VsAutomation80.DTE2)obj;
			}
		}
	}
}
