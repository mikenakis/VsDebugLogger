# VsDebugLogger<br><sup><sub>Keeps appending a text file to the debug output window of Visual Studio.</sup></sub>

*Because using debug-writeline is **350 times slower** than appending to a text file and flushing each line.*

# What is the problem

I am in favor of minimal logging; however, sometimes it happens that there is a lot of logging to be done. When this is the case, it really helps if the logging does not impose a serious performance penalty on the application that is doing the logging.

When I compare various approaches to logging on my machine, I observe the following:

- Emitting 1000 lines one-by-one via **System.Diagnostics.Debug.WriteLine()** takes a total of 1524 milliseconds. That is an absolutely terrifying 1.5 millisecond per line.

- Emitting 1000 lines one-by-one via a **System.IO.StreamWriter** into a **System.IO.FileStream** and invoking **Flush()** after each line takes a total of only 4.4 milliseconds. That is 4.4 microseconds per line, i.e. an astounding 350 times faster.

I do not care what are the technical or (quite likely) managerial excuses behind this situation, but to me it clearly means that some folks at Microsoft are incompetent dimwits who should be milking goats instead of trying to develop world-class software.

# What is the solution

VsDebugLogger fixes this problem. The idea is that we do all of our logging into a text file, (flushing each line to ensure that no line is lost if our application freezes,) and we have an external process running (it could also be a Visual Studio plugin) which keeps reading text as it is being appended to the file and emits that text to the debug output window of Visual Studio. This way, our application is only affected by the minimal performance overhead of writing to a log file, while Visual Studio can take all the time it wants to render and scroll its output window on its own threads.

# How to use VsDebugLogger

VsDebugLogger accepts one command-line parameter, which is a floating-point number representing the amount of time to wait between polls, in seconds.

Add the following function to your application:
```
	private static object setup_debug_logging( string solution_name, string log_file_pathname, //
			string path_to_vs_debug_logger )
	{
#if DEBUG
		System.Diagnostics.Process.Start( path_to_vs_debug_logger );
		var pipe = new System.IO.Pipes.NamedPipeClientStream( ".", "VsDebugLogger", //
				System.IO.Pipes.PipeDirection.InOut, SysIoPipes.PipeOptions.None );
		pipe.Connect( 1000 );
		System.IO.StreamWriter writer = new System.IO.StreamWriter( pipe );
		writer.WriteLine( $"LogFile solution={solution_name} file={log_file_pathname}" );
		writer.Flush();
		return pipe;
#else
		return null;
#endif
	}
```
Invoke the above function as early as possible during application startup, passing it the following:

- The name of your solution
- The full path name to the log file of your application.
- The full path name to VsDebugLogger.exe on your computer.

**Important:** Store the result in a member variable of your application so that it will stay alive until your application process ends.

# Status of the project

This utility is still work in progress, and at a very early stage of development. It is largely untested under real work conditions, so there may be situations where it does not work very well, or it does not work at all.

# License

Published under the MIT license. Do whatever you want with it.

# Contributions

There are a few potential areas of improvement where I could use some help:

- Create an installer or NuGet package (Never done this before, help would be welcome)
- Convert the standalone application into a Visual Studio extension (Never done this before, help would be welcome)
- Give some love to the application icon. (It could use some improvement from an actual artist rather than me I am just a programmer.)

If you do decide to contribute, please contact me first to arrange the specifics.

# TO DO list

- Handle spaces in names.
    - Currently, if the solution name or the log pathname contain any spaces, bad things will happen.
- Add back-tracking on error
    - If VsDebugLogger is unable to send a piece of text to Visual Studio, it should leave the log file offset where it was, so as to retry on the next tick.
- Handle pre-existing log content
    - Add the ability to indicate whether we want any existing content in a log file to be skipped or to be emitted to VisualStudio. (Currently we skip it, but that's probably not a good idea.)
- Add persisting and restoring of the window geometry across runs
- Get rid of settings as command-line arguments
    - Store them in some settings file. 
	- This is necessary because multiple different instances of VsDebugLogger may be launched from various applications in various solutions, but all these instances will immediately terminate except the one which was launched first, therefore the settings in effect will be whatever settings were passed to the first one launched, which is arbitrary.
- Display the currently active sessions in a list box
    - Possibly with statistics, like number of bytes logged so far, possibly even with an animated graph
- Once launched, make it minimize-to-tray
  - One point to keep in mind is that Microsoft seems to be making tray icons harder and harder to use; for example, Windows 11 hides all non-microsoft tray icons and you have to perform magical incantations to get it to show all tray icons.
  - See David Anson (Microsoft): "Get out of the way with the tray ["Minimize to tray" sample implementation for WPF]" https://dlaa.me/blog/post/9889700
  - See Stack Overflow: "C# trayicon using wpf" https://stackoverflow.com/q/12428006/773113
  - See Stack Overflow: "WPF applications tray icon [closed]" https://stackoverflow.com/q/41704392/773113
  - See Stack Overflow: "Determining location of tray icon" https://stackoverflow.com/q/4366449/773113
  - See Stack Overflow: "Can I use NotifyIcon in WPF?" https://stackoverflow.com/q/17674761/773113
  - See Code Project: "WPF NotifyIcon" https://www.codeproject.com/Articles/36468/WPF-NotifyIcon-2
  - See Microsoft Learn: "Notification Icon Sample" https://learn.microsoft.com/en-us/previous-versions/aa972170(v=vs.100)?redirectedfrom=MSDN
  - See possemeeg.wordpress.com: "Minimize to tray icon in WPF" https://possemeeg.wordpress.com/2007/09/06/minimize-to-tray-icon-in-wpf/
  - See Stack Overflow: "WPF Application that only has a tray icon" https://stackoverflow.com/q/1472633/773113
- Replace the logging text box with a virtual text box.
    - Because the text in there might become long.
- Display the log text inside VsDebugLogger
    - If we do this, then the following possibilities become available:
    	- When a log line is clicked, we can make VisualStudio go to the specific file and line using Visual Studio Automation.
    	- log line coloring per log level
		- margin indicators
		- Shorten the full pathnames of source files by stripping away the solution directory prefix from them.
		- show running counts of lines logged at different log levels
		- display an animated graph showing how the number-of-lines-logged-per-second varies.
		- display timestamps as:
    		- full UTC date-time string
			- offset from the moment the application was started
			- delta from the previous log line
		- provide audio feedback when lines of various levels are logged.
	- We could either merge all log files from a certain solution into one log display, (as per visual studio output window,) or show them in separate tabs.
- ~~Support launching of VsDebugLogger on demand~~ DONE
    - When an application launches, it should be able to somehow start VsDebugLogger if not already started.
- ~~Support multiple solutions and multiple log files per solution~~ - DONE
    - Maintain a named-pipe connection with each running application to negotiate which log file to monitor on behalf of that application and to know (via socket disconnection) when logging can pause.
- ~~Use a FileSystemWatcher instead of polling~~
    - Nope, this won't work. The use of FileSystemWatcher has a couple of severe disadvantages:
    	1. It won't work on network drives.
    		- Not really a problem in my case.
		1. It won't notify about changes in a file unless a handle to the file is opened or closed.
    		- Very much a problem in my case, because an application keeps a log file open while appending to it; log files are very rarely closed.
    		- See StackOverflow: "FileSystemWatcher changed event (for "LastWrite") is unreliable" https://stackoverflow.com/q/9563037/773113
- ~~Use FileInfo.Length instead of opening the file and invoking FileStream.Length~~ - DONE
    - This did not work at first, probably for the same reasons that FileSystemWatcher does not work. (The Windows File System refrains from updating this information unless a file handle is opened or closed.) However, I was able to make it work by performing a FileInfo.Refresh() before querying the length. It remains to be seen whether these two operations are faster than opening the file and querying the length of the open file.
- ~~Turn VsDebugLogger into a service~~
	- Actually, no, it should not be turned into a service, because services are useful for doing things while nobody is logged on, while this application is only useful to a logged-on user.
