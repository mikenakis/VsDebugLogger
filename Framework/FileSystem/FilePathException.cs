namespace Framework.FileSystem;

using Sys = Sys;

public class FilePathException : Sys.Exception
{
	public readonly FilePath FilePath;
	public readonly string OperationName;

	public FilePathException( Sys.Exception inner_exception, FilePath file_path, string operation_name )
			: base( inner_exception.Message, inner_exception )
	{
		FilePath = file_path;
		OperationName = operation_name;
		HResult = inner_exception.HResult;
	}

	public override string Message => $"Operation: {OperationName}; FilePath: {FilePath}; HResult=0x{InnerException!.HResult:X8}; \"{base.Message}\"";
}
