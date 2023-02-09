# VsDebugLogger<br><sup><sub>Keeps appending a text file to the debug output window of Visual Studio.</sup></sub>

*Because using debug-writeline is **350 times slower** than appending to a text file and flushing each line.*

# What is the problem

I am in favor of minimal logging; however, sometimes it happens that there is a lot of logging to be done. When this is the case, it really helps if the logging does not impose a serious performance penalty on the application that is doing the logging.

When I compare various approaches to logging on my machine, I observe the following:

- Emitting 1000 lines one-by-one via "System.Diagnostics.Debug.WriteLine()" takes a total of 1524 milliseconds. That is an absolutely terrifying 1.5 millisecond per line.

- Emitting 1000 lines one-by-one via a "System.IO.StreamWriter" into a "System.IO.FileStream" and invoking "Flush()" after every single line takes a total of only 4.4 milliseconds. That is 4.4 microseconds per line, i.e. 350 times faster.

I do not care what are the technical or (quite likely) managerial excuses behind this situation, but to me it clearly means that some folks at Microsoft are incompetent dimwits who should be milking goats instead of trying to develop world-class software.

# What is the solution

VsDebugLogger aims to fix this problem. The idea is that we do all of our logging to a text file, (flushing each line to ensure that no line is lost if our application freezes,) and we have an external process running (it could also be a Visual Studio plugin) which keeps reading text as it is being appended to the file and emits that text to the debug output window of Visual Studio. This way, our application is only affected by the minimal performance overhead of writing to a log file, while Visual Studio can take all the time it wants to render and scroll its output window on its own threads.

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
- Create an installer or NuGet package (Never done this before, help would be welcome)
- Convert the standalone application into a Visual Studio extension (Never done this before, help would be welcome)

If you do decide to contribute, please contact me first to arrange the specifics.


