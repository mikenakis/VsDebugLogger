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

When you run VsDebugLogger you supply 3 parameters:

- **file=**_filename_ is the full path name to your log file.
- **interval=**_floating-point-number_ specifies the amount of time to wait between polls, in seconds. If omitted, the default is 1 second. (This option may be deprecated later if I start using `FileSystemWatcher`.)
- **solution=**_name_ identifies which running instance of Visual Studio to send the output to, based on the name of the solution loaded by that running instance. If omitted, the instance will be picked at random.

# Status of the project

This utility is still work in progress, and at a very early stage of development. It is largely untested under real work conditions, so there may be situations where it does not work very well, or it does not work at all.

# License

Published under the MIT license. Do whatever you want with it.

# Contributions

There are many potential areas of improvement:

- Use a FileSystemWatcher instead of polling (I will be doing this soon)
- Make VsDebugLogger more available
  - Support launching of VsDebugLogger on demand
    - When an application launches, it should be able to somehow start VsDebugLogger if not already started.
  - Turn it into a service?
    - See Stack Overflow - "Using a FileSystemWatcher with Windows Service" - https://stackoverflow.com/q/30830565/773113 
	- Actually, no, it should not be turned into a service, because services are useful for doing things while nobody is logged on, while this application is only useful to a logged-on user.
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
- Support multiple applications
    - Maintain a socket (or named-pipe) connection with each running application to negotiate which log file to monitor on behalf of that application and to know (via socket disconnection) when logging can pause.
	- Then of course an interesting possibility is to transmit the log text via that connection so it is VsDebugLogger that also does the appending to the file. (And if VsDebugLogger is unavailable, then the application does the logging by itself.)
- Create an installer or NuGet package (Never done this before, help would be welcome)
- Convert the standalone application into a Visual Studio extension (Never done this before, help would be welcome)

If you do decide to contribute, please contact me first to arrange the specifics.


