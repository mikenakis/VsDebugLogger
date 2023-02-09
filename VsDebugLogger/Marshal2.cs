namespace VsDebugLogger;

using Sys = System;
using SysCodeAnalysis = System.Diagnostics.CodeAnalysis;
using SysRuntimeVersioning = System.Runtime.Versioning;
using SysSecurity = System.Security;
using SysInterop = System.Runtime.InteropServices;

//From Stack Overflow: "No definition found for GetActiveObject from System.Runtime.InteropServices.Marshal C#" https://stackoverflow.com/a/65496277/773113
[SysCodeAnalysis.SuppressMessage( "ReSharper", "IdentifierTypo" )]
[SysCodeAnalysis.SuppressMessage( "ReSharper", "CommentTypo" )]
[SysCodeAnalysis.SuppressMessage( "ReSharper", "InconsistentNaming" )]
public static class Marshal2
{
	internal const string OLEAUT32 = "oleaut32.dll";
	internal const string OLE32 = "ole32.dll";

	[SysSecurity.SecurityCritical] // auto-generated_required
	public static object GetActiveObject( string prog_id )
	{
		Sys.Guid clsid;
		// Call CLSIDFromProgIDEx first then fall back on CLSIDFromProgID if CLSIDFromProgIDEx doesn't exist.
		try
		{
			CLSIDFromProgIDEx( prog_id, out clsid );
		}
		catch( Sys.Exception )
		{
			CLSIDFromProgID( prog_id, out clsid );
		}
		GetActiveObject( ref clsid, Sys.IntPtr.Zero, out object? obj );
		return obj;
	}

	//[DllImport(Microsoft.Win32.Win32Native.OLE32, PreserveSig = false)]
	[SysInterop.DllImport( OLE32, PreserveSig = false )]
	[SysRuntimeVersioning.ResourceExposure( SysRuntimeVersioning.ResourceScope.None )]
	[SysSecurity.SuppressUnmanagedCodeSecurity]
	[SysSecurity.SecurityCritical]
	private static extern void CLSIDFromProgIDEx( [SysInterop.MarshalAs( SysInterop.UnmanagedType.LPWStr )] string prog_id, out Sys.Guid clsid );

	//[DllImport(Microsoft.Win32.Win32Native.OLE32, PreserveSig = false)]
	[SysInterop.DllImport( OLE32, PreserveSig = false )]
	[SysRuntimeVersioning.ResourceExposure( SysRuntimeVersioning.ResourceScope.None )]
	[SysSecurity.SuppressUnmanagedCodeSecurity]
	[SysSecurity.SecurityCritical]
	private static extern void CLSIDFromProgID( [SysInterop.MarshalAs( SysInterop.UnmanagedType.LPWStr )] string prog_id, out Sys.Guid clsid );

	//[DllImport(Microsoft.Win32.Win32Native.OLEAUT32, PreserveSig = false)]
	[SysInterop.DllImport( OLEAUT32, PreserveSig = false )]
	[SysRuntimeVersioning.ResourceExposure( SysRuntimeVersioning.ResourceScope.None )]
	[SysSecurity.SuppressUnmanagedCodeSecurity]
	[SysSecurity.SecurityCritical]
	private static extern void GetActiveObject( ref Sys.Guid rclsid, Sys.IntPtr reserved, [SysInterop.MarshalAs( SysInterop.UnmanagedType.Interface )] out object ppunk );
}
