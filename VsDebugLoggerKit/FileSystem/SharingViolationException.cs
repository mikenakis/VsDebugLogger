namespace VsDebugLoggerKit.FileSystem;

using SysIo = System.IO;

// "The process cannot access the file '{X}' because it is being used by another process."
public class SharingViolationException : FilePathException
{
	public SharingViolationException( SysIo.IOException innerException, FilePath filePath, string operationName )
			: base( innerException, filePath, operationName )
	{ }
}
