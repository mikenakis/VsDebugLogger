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

	public CommandlineArgumentParser( string[] argumentsArray )
			: this( new List<string>( argumentsArray ) )
	{ }

	public CommandlineArgumentParser( List<string> arguments )
	{
		this.arguments = arguments;
		string? additionalArgumentsFileName = TryExtractOption( "argumentFile" );
		if( additionalArgumentsFileName != null )
		{
			FilePath filePath = FilePath.FromRelativeOrAbsolutePath( additionalArgumentsFileName );
			IEnumerable<string> lines = filePath //
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
	public string? TryExtractArgumentByPrefix( string argumentPrefix )
	{
		int index = find( arguments, argumentPrefix );
		if( index == -1 )
			return null;
		string argument = arguments.ExtractAt( index );
		return argument.Substring( argumentPrefix.Length );

		static int find( IEnumerable<string> args, string argumentName )
		{
			int i = 0;
			foreach( var arg in args )
			{
				if( arg.StartsWith( argumentName, Sys.StringComparison.Ordinal ) )
					return i;
				i++;
			}
			return -1;
		}
	}

	///Tries to find an option of the form name=value in the given list of arguments, extracts it from the list, and returns the value. Returns `null` if not found.
	public string? TryExtractOption( string argumentName )
	{
		return TryExtractArgumentByPrefix( argumentName + "=" );
	}

	///Finds an option of the form name=value in the given list of arguments, extracts it from the list, and returns the value. Returns the supplied default if not found, fails if the default is `null`.
	public string ExtractOption( string argumentName, string? defaultValue = null )
	{
		string? value = TryExtractOption( argumentName );
		if( value == null )
		{
			if( defaultValue == null )
				throw new Sys.ApplicationException( $"Expected '{argumentName}='" );
			return defaultValue;
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
