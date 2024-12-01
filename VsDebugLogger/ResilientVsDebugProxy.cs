namespace VsDebugLogger;

using SysInterop = global::System.Runtime.InteropServices;
using global::System.Collections.Generic;
using global::System.Linq;
using static global::VsDebugLoggerKit.Statics;
using Log = global::VsDebugLoggerKit.Logging.Log;
using System;
using VsInterop = Microsoft.VisualStudio.OLE.Interop;
using VsAutomation80 = EnvDTE80;
using VsAutomation = EnvDTE;
using VsDebugLoggerKit.FileSystem;

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
			string? thisSolutionName = get_solution_name_from_vs_instance( vsInstance );
			if( thisSolutionName == solutionName )
				return vsInstance;
		}
		Log.Error( $"No running instance of Visual Studio has solution '{solutionName}' open." );
		return null;
	}

	private static string? get_solution_name_from_vs_instance( VsAutomation80.DTE2 vsInstance )
	{
		//PEARL: The 'EnvDTE.Solution.FullName' property does not contain a "full name", it actually contains the full pathname to the solution.
		//       Therefore, we cannot use this property; instead, we use EnvDTE.Solution.Properties.Item( "Name" ).Value.
		//       Check out Microsoft's despicable corp-talk about this here:
		//           Visual Studio Developer Community - Question: "DTE2 Solution.Fullname return a filepath" - Resolution: "Closed - Not a Bug"
		//           https://developercommunity.visualstudio.com/t/dte2-solutionfullname-return-a-filepath/43005
		if( False )
		{
			FilePath solutionFilePath = FilePath.FromAbsolutePath( vsInstance.Solution.FullName );
			return solutionFilePath.GetFileNameWithoutExtension();
		}
		else
		{
			//PEARL: If one of the currently open instances of Visual Studio happens have no solution open,
			//       its `DTE2.Solution` property will still return a perfectly valid solution object,
			//       and this object will have a perfectly valid `Properties` collection,
			//       and this collection will contain a perfectly valid "Name" item,
			//       and if you try to get the `Value` of that item you will be slapped in the face with
			//       a System.Runtime.InteropServices.COMException which tells you absolutely nothing as
			//       to what is wrong.
			//       (Unless you consider "Exception occurred. (0x80020009 (DISP_E_EXCEPTION))" to be an
			//       explanation as to what is wrong.)
			//       To avoid this, we have to check the `IsOpen` property of the solution.
			VsAutomation.Solution? solution = vsInstance.Solution;
			if( !solution.IsOpen )
				return null;
			VsAutomation.Properties solutionProperties = solution.Properties;
			VsAutomation.Property property = solutionProperties.Item( "Name" );
			return (string)property.Value;
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
			{
				//PEARL: Unless Visual Studio has already taken some action which required the debug output pane to be shown,
				//       the pane will be invisible AND unavailable for selection in the "Show output from:" drop-down list.
				//       Thus, not only will the user not see the text that we write to that pane, but also, the user will
				//       not even be able to select the pane from the drop down list so as to see the text.
				//       Normally this cannot happen, because Visual Studio will open the debug output pane when launching
				//       our application, but it may happen by mistake, if solution A connects to VsDebugLogger
				//       providing a wrong solution name, which happens to be the name of solution B, which is currently open
				//       in another instance of Visual Studio, and debugging has not been attempted at least once in that other
				//       instance.
				//       Under such a scenario, VsDebugLogger will be sending the log output of solution A into the debug output
				//       window of the instance of Visual Studio with solution B, and if the user could see this they would
				//       immediately know what mistake they made, but the user will not be able to see it.
				//       The magical incantation which solves this problem is to "Activate" the debug output pane, thus ensuring
				//       that the pane is visible to the user, or at the very least selectable for viewing by the user.
				try
				{
					outputWindowPane.Activate();
				}
				catch( Exception ex )
				{
					Log.Warn( "outputWindowPane.Activate() failed.", ex );
				}
				return outputWindowPane;
			}
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
