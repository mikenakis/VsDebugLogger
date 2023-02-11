namespace Framework;

using System.Collections.Generic;
using System.Linq;
using Framework.Extensions;
using Framework.FileSystem;
using Sys = Sys;

public class CommandlineArgumentParser
{
	private readonly List<string> arguments;
	public bool PauseOption { get; }
	public bool NonEmpty => arguments.Count != 0;
	public string AllRemainingArguments => arguments.MakeString( " " );

	public CommandlineArgumentParser( string[] arguments_array )
			: this( new List<string>( arguments_array ) )
	{ }

	public CommandlineArgumentParser( List<string> arguments )
	{
		this.arguments = arguments;
		string? additional_arguments_file_name = TryExtractOption( "argumentFile" );
		if( additional_arguments_file_name != null )
		{
			FilePath file_path = FilePath.FromRelativeOrAbsolutePath( additional_arguments_file_name );
			IEnumerable<string> lines = file_path //
					.ReadLines() //
					.Select( stripComment ) //
					.Select( s => s.Trim() ) //
					.Where( s => s.Length > 0 );
			arguments.AddRange( lines );
		}

		PauseOption = ExtractSwitch( "--pause" );

		static string stripComment( string line )
		{
			int i = line.IndexOf( '#' );
			if( i == -1 )
				return line;
			return line[..i];
		}
	}

	///Tries to find an argument by prefix in the given list of arguments, extracts it from the list, and returns the remainder after the prefix. Returns `null` if not found.
	public string? TryExtractArgumentByPrefix( string argument_prefix )
	{
		int index = find( arguments, argument_prefix );
		if( index == -1 )
			return null;
		string argument = arguments.ExtractAt( index );
		return argument.Substring( argument_prefix.Length );

		static int find( IEnumerable<string> args, string argument_name )
		{
			int i = 0;
			foreach( var arg in args )
			{
				if( arg.StartsWith( argument_name, Sys.StringComparison.Ordinal ) )
					return i;
				i++;
			}
			return -1;
		}
	}

	///Tries to find an option of the form name=value in the given list of arguments, extracts it from the list, and returns the value. Returns `null` if not found.
	public string? TryExtractOption( string argument_name )
	{
		return TryExtractArgumentByPrefix( argument_name + "=" );
	}

	///Finds an option of the form name=value in the given list of arguments, extracts it from the list, and returns the value. Returns the supplied default if not found, fails if the default is `null`.
	public string ExtractOption( string argument_name, string? default_value = null )
	{
		string? value = TryExtractOption( argument_name );
		if( value == null )
		{
			if( default_value == null )
				throw new Sys.ApplicationException( $"Expected '{argument_name}='" );
			return default_value;
		}
		return value;
	}

	///Finds a switch of the form name[=true|=false] in the given list of arguments, extracts it from the list, and
	///returns the value. Returns `false` if the name not found; returns `true` if the name is found but the value is
	///not supplied.
	public bool ExtractSwitch( string prefix )
	{
		if( arguments.Remove( prefix ) )
			return true;
		string? argument = TryExtractOption( prefix );
		if( argument == null )
			return false;
		return parse_bool( argument );
	}

	// PEARL: When `bool.Parse( string )` throws a System.FormatException it says "String was not recognized as a valid
	// Boolean" BUT IT DOES NOT SAY WHAT THE STRING WAS. The following method corrects this imbecility.
	private static bool parse_bool( string argument )
	{
		if( bool.TryParse( argument, out bool result ) )
			return result;
		throw new Sys.Exception( $"Expected 'true' or 'false', found '{argument}'." );
	}
}
