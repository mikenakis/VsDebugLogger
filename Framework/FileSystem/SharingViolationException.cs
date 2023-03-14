namespace Framework.FileSystem;

using System.IO;

// "The process cannot access the file '{X}' because it is being used by another process."
public class SharingViolationException : FilePathException
{
	public SharingViolationException( IOException innerException, FilePath filePath, string operationName )
			: base( innerException, filePath, operationName )
	{ }
}
