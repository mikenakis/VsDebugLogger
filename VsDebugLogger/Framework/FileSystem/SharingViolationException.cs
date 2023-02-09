namespace VsDebugLogger.Framework.FileSystem;

using System.IO;

// "The process cannot access the file '{X}' because it is being used by another process."
public class SharingViolationException : FilePathException
{
	public SharingViolationException( IOException inner_exception, FilePath file_path, string operation_name )
			: base( inner_exception, file_path, operation_name )
	{ }
}
