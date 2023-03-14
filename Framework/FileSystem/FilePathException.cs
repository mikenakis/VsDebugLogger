namespace Framework.FileSystem;

using Sys = Sys;

public class FilePathException : Sys.Exception
{
	public readonly FilePath FilePath;
	public readonly string OperationName;

	public FilePathException( Sys.Exception innerException, FilePath filePath, string operationName )
			: base( innerException.Message, innerException )
	{
		FilePath = filePath;
		OperationName = operationName;
		HResult = innerException.HResult;
	}

	public override string Message => $"Operation: {OperationName}; FilePath: {FilePath}; HResult=0x{InnerException!.HResult:X8}; \"{base.Message}\"";
}
