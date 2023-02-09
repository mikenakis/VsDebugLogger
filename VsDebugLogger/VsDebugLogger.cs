namespace VsDebugLogger;

using System.Windows.Threading;
using VsDebugLogger.Framework;
using VsDebugLogger.Framework.FileSystem;
using SysGlob = System.Globalization;
using Sys = System;
using SysIo = System.IO;
using static Statics;

internal class TheVsDebugLogger
{
	private static readonly Sys.TimeSpan default_interval = Sys.TimeSpan.FromSeconds( 1.0 );
	private readonly Procedure<string> logger;
	private readonly DispatcherTimer wpf_timer = new();
	private readonly ResilientVsDebugProxy debug_pane;
	private readonly ResilientInputStream resilient_input_stream;

	//These might become configurable via the gui
	// ReSharper disable FieldCanBeMadeReadOnly.Local
	// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

	private FilePath file_path;
	private Sys.TimeSpan interval;
	private string solution_name;

	// ReSharper restore PrivateFieldCanBeConvertedToLocalVariable
	// ReSharper restore FieldCanBeMadeReadOnly.Local

	//TODO: select specific instance of visual studio
	//See https://stackoverflow.com/questions/14205933/how-do-i-get-the-dte-for-running-visual-studio-instance

	public TheVsDebugLogger( string[] program_arguments, Procedure<string> logger )
	{
		this.logger = logger;

		CommandlineArgumentParser commandline_argument_parser = new CommandlineArgumentParser( program_arguments );

		string file_path_as_string = commandline_argument_parser.ExtractOption( "file" );
		if( !SysIo.Path.IsPathFullyQualified( file_path_as_string ) )
			throw new Sys.ApplicationException( $"Expected a fully qualified pathname, got '{file_path_as_string}'." );
		file_path = FilePath.FromAbsolutePath( file_path_as_string );

		string interval_as_string = commandline_argument_parser.ExtractOption( "interval", default_interval.TotalSeconds.ToString( SysGlob.CultureInfo.InvariantCulture ) );
		const SysGlob.NumberStyles options = SysGlob.NumberStyles.Float | SysGlob.NumberStyles.AllowExponent | SysGlob.NumberStyles.AllowDecimalPoint;
		if( !double.TryParse( interval_as_string, options, SysGlob.NumberFormatInfo.InvariantInfo, out double interval_as_seconds ) )
			throw new Sys.ApplicationException( $"Expected a (fractional) number of seconds, got '{interval_as_string}'." );
		interval = Sys.TimeSpan.FromSeconds( interval_as_seconds );

		solution_name = commandline_argument_parser.ExtractOption( "solution", "" );

		log( $"Polling every {interval.TotalSeconds} seconds" );
		log( $"Reading from '{file_path}'" );
		log( $"Appending to the debug output window of solution '{solution_name}'." );

		debug_pane = new ResilientVsDebugProxy( solution_name, logger );
		resilient_input_stream = new ResilientInputStream( file_path, logger );

		wpf_timer.Interval = interval;
		wpf_timer.Tick += on_timer_tick;
		wpf_timer.Start();
	}

	private void log( string text ) => logger.Invoke( text );

	private void on_timer_tick( object? sender, Sys.EventArgs e )
	{
		Assert( sender == wpf_timer );
		Assert( wpf_timer.IsEnabled );

		try
		{
			if( !resilient_input_stream.ReadNext( out string text ) )
				return;
			if( text == "" )
				return;
			if( !debug_pane.Write( text ) )
				return;
			log( "Appended text." );
		}
		catch( Sys.Exception exception )
		{
			log( $"Unexpected exception: {exception.GetType()}: {exception.Message}" );
		}
	}
}
