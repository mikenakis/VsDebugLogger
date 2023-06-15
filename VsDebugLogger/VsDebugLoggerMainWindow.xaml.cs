namespace VsDebugLogger;

using SysThread = System.Threading;
using Framework;
using Framework.Logging;
using Sys = System;
using Wpf = System.Windows;
using WinForms = System.Windows.Forms;
using WinFormsDrawing = System.Drawing;
using SysCompModel = System.ComponentModel;

//PEARL: Windows Tray Icons are a very sorry affair nowadays.
//       Every new release of Windows seems to:
//         - Hide them farther and farther away from view,
//         - Make it more and more difficult for the user to make them visible.
//       WPF has no support for them, forcing us to use WinForms functionality.
//       The WinForms functionality looks like it used to work in the past, but most of it does not work anymore:
//       - It is impossible to differentiate between a left-click and a right-click on a tray icon.
//       - The tray icon ignores its context menu. If you create a context menu, and open it, then it appears at
//         the top-left corner of the screen, which means that it is not actually associated with the tray icon.
//       - The tray icon will not show a balloon tip unless some legacy registry key has been created.
//Tried setting HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\EnableBalloonTips to 1, no difference.
//Tried setting HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Explorer\EnableLegacyBalloonNotifications to 1, it works.
public partial class VsDebugLoggerMainWindow : Wpf.Window
{
	private readonly WinForms.NotifyIcon trayIcon;

	public VsDebugLoggerMainWindow()
	{
		GlobalLogger.Instance = DistributingLogger.Of( DebugLogger.Instance, log );
		InitializeComponent();
		trayIcon = new WinForms.NotifyIcon();
		trayIcon.Text = DotNetHelpers.GetProductName() + " is running.";
		trayIcon.Icon = new WinFormsDrawing.Icon( DotNetHelpers.GetResource( "VsDebugLogger.ico", GetType() ) );
		//PEARL: the Click event is exactly the same as the MouseClick event, and it fires regardless of whether you left-click or right-click!
		//trayIcon.Click += onTrayIconClick;
		trayIcon.MouseClick += onTrayIconClick;
		//PEARL: the context menu appears at the top-left corner of the screen, so it is as if the tray icon knows nothing about it.
		trayIcon.ContextMenuStrip = new WinForms.ContextMenuStrip();
		trayIcon.ContextMenuStrip.Text = DotNetHelpers.GetProductName() + " Menu";
		trayIcon.ContextMenuStrip.Items.Add( new WinForms.ToolStripMenuItem( "File" ) );
		Loaded += onLoaded;
	}

	void onLoaded( object sender, Wpf.RoutedEventArgs e )
	{
		WindowState = Wpf.WindowState.Minimized;
	}

	protected override void OnActivated( Sys.EventArgs eventArgs )
	{
		Log.Debug( "OnActivated()" );
		base.OnActivated( eventArgs );
	}

	protected override void OnClosed( Sys.EventArgs eventArgs )
	{
		Log.Debug( "OnClosed()" );
		base.OnClosed( eventArgs );
		trayIcon.Dispose();
	}

	private Wpf.WindowState previousWindowState = Wpf.WindowState.Normal;

	protected override void OnStateChanged( Sys.EventArgs eventArgs )
	{
		Log.Debug( "OnStateChanged()" );
		base.OnStateChanged( eventArgs );
		if( WindowState == Wpf.WindowState.Minimized )
		{
			trayIcon.Visible = true;
			// trayIcon.BalloonTipText = $"{DotNetHelpers.GetProductName()} has been minimized. Click the tray icon to show.";
			// trayIcon.BalloonTipTitle = DotNetHelpers.GetProductName();
			// trayIcon.BalloonTipIcon = WinForms.ToolTipIcon.Info;
			// trayIcon.ShowBalloonTip( 30 * 1000 );
			Hide();
		}
		else
		{
			previousWindowState = WindowState;
			trayIcon.Visible = false;
		}
	}

	void onTrayIconClick( object? sender, Sys.EventArgs e )
	{
		Log.Debug( "onTrayIconClick()" );
		//trayIcon.ContextMenuStrip!.Show();
		Show();
		WindowState = previousWindowState;
	}

	private void log( LogEntry logEntry )
	{
		if( logEntry.Level == LogLevel.Debug )
			return;
		string text = logEntry.Level + ": " + logEntry.Message + "\r\n";
		StatusText.Text += text;
		StatusText.CaretIndex = StatusText.Text.Length;
		StatusText.ScrollToEnd();
	}
}
