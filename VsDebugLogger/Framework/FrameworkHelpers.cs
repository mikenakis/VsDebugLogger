namespace VsDebugLogger.Framework;

using System.Linq;
using Sys = System;
using SysText = System.Text;
using SysGlob = System.Globalization;
using static Statics;

public static class FrameworkHelpers
{
	///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// String

	public static string SafeToString( object? value )
	{
		if( value == null )
		{
			//PEARL: In C# if you convert a null object to string, instead of "null" you get the empty string!
			//       Thus, if you print the value of a string, you never know whether the value was the empty string or null.
			//       I guess this is happening because the DotNet Common Language Runtime (CLR) is shared among various languages besides C#,
			//       and they did not want to ruffle the feathers of any Visual Basic programmers by showing them evil-looking unknown words like 'null'.
			//       We fix this here.
			Assert( $"{value}" == "" ); //This ensures that the current behavior is the known retarded behavior.
			return "null";
		}
		Sys.Type type = value.GetType();
		if( type.IsEnum || type.IsPrimitive )
			return $"{value}";
		if( type == typeof(string) )
			return EscapeForCSharp( (string)value );
		try
		{
			return "{" + value + "}";
		}
		catch( Sys.Exception e )
		{
			return $"(ToString() threw {e.GetType().FullName})";
		}
	}

	public static string EscapeForCSharp( string content ) => EscapeForCSharp( content, '"' );

	public static string EscapeForCSharp( char content ) => EscapeForCSharp( content.ToString(), '\'' );

	public static string EscapeForCSharp( string content, char? quote )
	{
		var builder = new SysText.StringBuilder();
		if( quote.HasValue )
			builder.Append( quote.Value );
		foreach( char c in content )
		{
			switch( c )
			{
				case '\b':
					builder.Append( "\\b" );
					break;
				case '\t':
					builder.Append( "\\t" );
					break;
				case '\f':
					builder.Append( "\\f" );
					break;
				case '\r':
					builder.Append( "\\r" );
					break;
				case '\n':
					builder.Append( "\\n" );
					break;
				case '\\':
					builder.Append( "\\\\" );
					break;
				default:
					if( c == quote )
						builder.Append( '\\' ).Append( c );
					else if( !IsPrintable( c ) )
					{
						if( c < 256 ) // no need to check for >= 0 because char is unsigned.
							appendTwoDigits( builder.Append( "\\x" ), c );
						else
							appendFourDigits( builder.Append( "\\u" ), c );
					}
					else
						builder.Append( c );
					break;
			}
		}
		if( quote.HasValue )
			builder.Append( quote.Value );
		return builder.ToString();

		static void appendTwoDigits( SysText.StringBuilder builder, char c )
		{
			builder.Append( digitFromNibble( c >> 4 ) );
			builder.Append( digitFromNibble( c & 0x0f ) );
		}

		static void appendFourDigits( SysText.StringBuilder builder, char c )
		{
			builder.Append( digitFromNibble( (c >> 12) & 0x0f ) );
			builder.Append( digitFromNibble( (c >> 8) & 0x0f ) );
			builder.Append( digitFromNibble( (c >> 4) & 0x0f ) );
			builder.Append( digitFromNibble( c & 0x0f ) );
		}

		static char digitFromNibble( int nybble )
		{
			Assert( nybble is >= 0 and < 16 );
			if( nybble >= 10 )
				return (char)('a' + nybble - 10);
			return (char)('0' + nybble);
		}
	}

	public static bool IsPrintable( char c )
	{
		if( c >= 32 && c < 127 )
			return true;
		switch( char.GetUnicodeCategory( c ) )
		{
			case SysGlob.UnicodeCategory.UppercaseLetter:
			case SysGlob.UnicodeCategory.LowercaseLetter:
			case SysGlob.UnicodeCategory.TitlecaseLetter:
			case SysGlob.UnicodeCategory.DecimalDigitNumber:
			case SysGlob.UnicodeCategory.LetterNumber:
			case SysGlob.UnicodeCategory.OtherNumber:
				return true;
			case SysGlob.UnicodeCategory.ModifierLetter:
			case SysGlob.UnicodeCategory.OtherLetter:
			case SysGlob.UnicodeCategory.NonSpacingMark:
			case SysGlob.UnicodeCategory.SpacingCombiningMark:
			case SysGlob.UnicodeCategory.EnclosingMark:
			case SysGlob.UnicodeCategory.SpaceSeparator:
			case SysGlob.UnicodeCategory.LineSeparator:
			case SysGlob.UnicodeCategory.ParagraphSeparator:
			case SysGlob.UnicodeCategory.Control:
			case SysGlob.UnicodeCategory.Format:
			case SysGlob.UnicodeCategory.Surrogate:
			case SysGlob.UnicodeCategory.PrivateUse:
			case SysGlob.UnicodeCategory.ConnectorPunctuation:
			case SysGlob.UnicodeCategory.DashPunctuation:
			case SysGlob.UnicodeCategory.OpenPunctuation:
			case SysGlob.UnicodeCategory.ClosePunctuation:
			case SysGlob.UnicodeCategory.InitialQuotePunctuation:
			case SysGlob.UnicodeCategory.FinalQuotePunctuation:
			case SysGlob.UnicodeCategory.OtherPunctuation:
			case SysGlob.UnicodeCategory.MathSymbol:
			case SysGlob.UnicodeCategory.CurrencySymbol:
			case SysGlob.UnicodeCategory.ModifierSymbol:
			case SysGlob.UnicodeCategory.OtherSymbol:
			case SysGlob.UnicodeCategory.OtherNotAssigned:
				return false;
			default: //
				return false;
		}
	}

	///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// System.Type

	// Obtains the full name of a type using C# notation.
	// PEARL: DotNet represents the full names of types in a cryptic way which does not correspond to any language in particular:
	//        - Generic types are suffixed with a back-quote character, followed by the number of generic parameters.
	//        - Constructed generic types are further suffixed with a list of assembly-qualified type names, one for each generic parameter.
	//        Plus, a nested class is denoted with the '+' sign. (Handling of which is TODO.)
	//        This method returns the full name of a type using C#-specific notation instead of DotNet's unwanted notation.
	public static string GetCSharpTypeName( Sys.Type type )
	{
		if( type.IsArray )
		{
			SysText.StringBuilder string_builder = new SysText.StringBuilder();
			string_builder.Append( GetCSharpTypeName( NotNull( type.GetElementType() ) ) );
			string_builder.Append( "[" );
			int rank = type.GetArrayRank();
			Assert( rank >= 1 );
			for( int i = 0; i < rank - 1; i++ )
				string_builder.Append( "," );
			string_builder.Append( "]" );
			return string_builder.ToString();
		}
		else if( type.IsGenericType )
		{
			SysText.StringBuilder string_builder = new SysText.StringBuilder();
			string_builder.Append( getBaseTypeName( type ) );
			string_builder.Append( '<' );
			string_builder.Append( type.GenericTypeArguments.Select( GetCSharpTypeName ).MakeString( "," ) );
			string_builder.Append( '>' );
			return string_builder.ToString();
		}
		else
			return type.Namespace + '.' + type.Name.Replace( '+', '.' );

		static string getBaseTypeName( Sys.Type type )
		{
			string type_name = NotNull( type.GetGenericTypeDefinition().FullName );
			int index_of_tick = type_name.LastIndexOf( '`' );
			Assert( index_of_tick == type_name.IndexOf( '`' ) );
			return type_name.Substring( 0, index_of_tick );
		}
	}
}
