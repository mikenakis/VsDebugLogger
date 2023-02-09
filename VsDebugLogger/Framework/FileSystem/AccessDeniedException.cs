﻿namespace VsDebugLogger.Framework.FileSystem;

using System.IO;

// Indicates that the process insufficient privileges to access a certain resource.  (The caller does not have the required permission.)
// For example, trying to read from a file without the necessary read permission.
// PEARL: this "Access Denied" error may also be reported in various other fundamentally different situations, such as:
//   - Trying to access an executable file that is in use. (This is not a permissions error, it is a sharing violation.)
//   - Trying to open a file which is in fact a directory. (This is not a permissions error, it is a "this is not what you think it is" error.)
//   - Trying to write a read-only file. (This is not a permissions error, it is a "file is not even writable" error.)
public class AccessDeniedException : FilePathException
{
	public AccessDeniedException( IOException inner_exception, FilePath file_path, string operation_name )
			: base( inner_exception, file_path, operation_name )
	{ }
}
