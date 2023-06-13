namespace Framework;

using Sys = global::System;
using SysIo = global::System.IO;
using SysGlob = global::System.Globalization;
using SysText = global::System.Text;
using SysDiag = global::System.Diagnostics;
using Legacy = global::System.Collections;
using SysReflect = System.Reflection;
using global::System.Collections.Generic;
using global::System.Collections.Immutable;
using global::System.Linq;
using Math = global::System.Math;
using static global::Framework.Statics;
using Log = global::Framework.Logging.Log;
using Framework.Extensions;
using Framework.FileSystem;
using SysNet = System.Net;
using SysComp = System.Runtime.CompilerServices;

public static class DotNetHelpers
{
	//See https://stackoverflow.com/q/616584/773113
	public static string GetProductName()
	{
		string name = Sys.AppDomain.CurrentDomain.FriendlyName;
		return SysIo.Path.GetFileNameWithoutExtension( name );
	}

	public static long GetProcessPrivateMemory()
	{
		var currentProcess = SysDiag.Process.GetCurrentProcess();
		return currentProcess.PrivateMemorySize64;
	}

	public static Sys.Exception Exit( int exitCode )
	{
		Sys.Environment.Exit( exitCode );
		return new Sys.Exception();
	}

	[SysDiag.Conditional( "DEBUG" )]
	public static void PerformGarbageCollection()
	{
		const int maxRetries = 10;
		Sys.DateTime startTime = Sys.DateTime.UtcNow;
		long startMemory = GetProcessPrivateMemory();
		int retries = perform_garbage_collection( maxRetries );
		long memoryDifference = startMemory - GetProcessPrivateMemory();
		Sys.TimeSpan duration = Sys.DateTime.UtcNow - startTime;
		Log.Debug( $"Garbage collection completed after {retries} retries, {duration.TotalSeconds:F2} seconds; {Math.Abs( memoryDifference )} {(memoryDifference < 0 ? "lost" : "reclaimed")}." );
	}

	public static string MakeTechnicalTimeStamp( Sys.DateTime utcTime, Sys.TimeZoneInfo localTz )
	{
		Sys.TimeSpan offset = localTz.GetUtcOffset( utcTime );
		Sys.DateTime localTime = Sys.TimeZoneInfo.ConvertTimeFromUtc( utcTime, localTz );
		return $"{localTime.Year:D4}-{localTime.Month:D2}-{localTime.Day:D2} {localTime.Hour:D2}:{localTime.Minute:D2}:{localTime.Second:D2} GMT{offset.TotalHours:+#;-#;+0}";
	}

	private static object garbageCollectedObject = new object();

	private static int perform_garbage_collection( int retryCount )
	{
		Sys.WeakReference reference = create_weak_reference();
		for( int retry = 0;; retry++ )
		{
			Sys.GC.Collect( 0xffff, Sys.GCCollectionMode.Forced, true );
			Sys.GC.WaitForPendingFinalizers();
			if( !reference.IsAlive || retry >= retryCount )
				return retry;
		}
	}

	private static Sys.WeakReference create_weak_reference()
	{
		if( True )
		{
			object obj = garbageCollectedObject;
			garbageCollectedObject = new object();
			return new Sys.WeakReference( obj );
		}
		else
		{
			// PEARL: this needs to be done in a separate method because the C# compiler anchors `new object()` to the executing method's stack frame even though no local variable is declared.
			return new Sys.WeakReference( new object() );
		}
	}

	public static Sys.DateTime UnixTimeStampToDateTime( double unixTimeStamp )
	{
		// Unix timestamp is seconds past epoch
		System.DateTime dtDateTime = new Sys.DateTime( 1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc );
		dtDateTime = dtDateTime.AddTicks( (long)(unixTimeStamp * 1e7) );
		return dtDateTime;
	}

	public static double ToUnixTimeStamp( Sys.DateTime dateTime ) => dateTime.Subtract( new Sys.DateTime( 1970, 1, 1, 0, 0, 0, 0, Sys.DateTimeKind.Utc ) ).TotalSeconds;

	public static string MakeTimeZoneDisplayName( Sys.TimeSpan timeSpan )
	{
		return $"GMT{timeSpan.Hours:+#;-#;+0}:{timeSpan.Minutes:D2}";
	}

	public static FilePath GetMainModuleFilePath()
	{
		SysDiag.ProcessModule mainModule = NotNull( SysDiag.Process.GetCurrentProcess().MainModule );
		// PEARL: System.Diagnostics.Process.GetCurrentProcess().MainModule.Filename is not a filename, it is a pathname!
		string fullPathName = mainModule.FileName;
		return FilePath.FromAbsolutePath( fullPathName );
	}

	public static DirectoryPath GetMainModuleDirectoryPath()
	{
		return GetMainModuleFilePath().Directory;
	}

	private static string? mainModuleName;
	public static string MainModuleName => mainModuleName ??= get_main_module_name();

	private static string get_main_module_name()
	{
		string name = NotNull( SysDiag.Process.GetCurrentProcess().MainModule ).ModuleName;
		const string exeExtension = ".exe";
		Assert( name.EndsWith( exeExtension, Sys.StringComparison.OrdinalIgnoreCase ) );
		return name[..^exeExtension.Length];
	}

	///<summary>Returns something like C:\Users\(UserName)\Documents (which used to be called "My Documents")</summary>
	public static DirectoryPath UserDocumentsFolder => DirectoryPath.FromAbsolutePath( Sys.Environment.GetFolderPath( Sys.Environment.SpecialFolder.MyDocuments ) );

	private static DirectoryPath? applicationLocalAppDataFolder;

	///<summary>Returns something like C:\Users\(UserName)\AppData\Local\(ApplicationName)</summary>
	public static DirectoryPath UserAppDataLocalApplicationFolder => applicationLocalAppDataFolder ??= get_application_local_app_data_folder();

	private static DirectoryPath get_application_local_app_data_folder()
	{
		DirectoryPath localAppDataFolder = DirectoryPath.FromAbsolutePath( Sys.Environment.GetFolderPath( Sys.Environment.SpecialFolder.LocalApplicationData ) );
		DirectoryPath folderName = localAppDataFolder.SubDirectory( MainModuleName );
		folderName.CreateIfNotExist(); // necessary on the first run after a fresh installation.
		return folderName;
	}

	public static byte[] ReadAll( SysIo.Stream self )
	{
		var buffers = new List<byte[]>();
		for( ;; )
		{
			var fixedSizeBuffer = new byte[1024 * 1024];
			int length = self.Read( fixedSizeBuffer, 0, fixedSizeBuffer.Length );
			if( length == 0 )
				break;
			var properSizeBuffer = new byte[length];
			Sys.Array.Copy( fixedSizeBuffer, properSizeBuffer, length );
			buffers.Add( properSizeBuffer );
		}
		switch( buffers.Count )
		{
			case 0: return Sys.Array.Empty<byte>();
			case 1: return buffers[0];
			default:
			{
				int totalLength = 0;
				foreach( byte[] buffer in buffers )
					totalLength += buffer.Length;
				byte[] result = new byte[totalLength];
				int offset = 0;
				foreach( byte[] buffer in buffers )
				{
					Sys.Array.Copy( buffer, 0, result, offset, buffer.Length );
					offset += buffer.Length;
				}
				return result;
			}
		}
	}

	public static void CopyTo( SysIo.DirectoryInfo self, SysIo.DirectoryInfo targetDirectory, Function<bool, SysIo.FileSystemInfo, SysIo.FileSystemInfo> filter )
	{
		foreach( SysIo.FileSystemInfo fileSystemInfo in self.EnumerateFileSystemInfos( "*", SysIo.SearchOption.TopDirectoryOnly ) )
		{
			switch( fileSystemInfo )
			{
				case SysIo.FileInfo sourceFile:
				{
					var targetFile = new SysIo.FileInfo( SysIo.Path.Combine( targetDirectory.FullName, sourceFile.Name ) );
					if( !filter( sourceFile, targetFile ) )
						continue;
					SysIo.Directory.CreateDirectory( targetDirectory.FullName );
					sourceFile.CopyTo( targetFile.FullName, true );
					break;
				}
				case SysIo.DirectoryInfo sourceSubDirectory:
				{
					string combined = SysIo.Path.Combine( targetDirectory.FullName, sourceSubDirectory.Name );
					var targetSubDirectory = new SysIo.DirectoryInfo( combined );
					if( !filter( sourceSubDirectory, targetSubDirectory ) )
						continue;
					CopyTo( sourceSubDirectory, targetSubDirectory, filter );
					break;
				}
				default:
					Assert( false ); //what do we do with this?
					break;
			}
		}
	}

	public static double ParseDouble( string s )
	{
		if( TryParseDouble( s, out double value ) )
			return value;
		throw new Sys.FormatException( s );
	}

	public static bool TryParseDouble( string s, out double value )
	{
		const SysGlob.NumberStyles numberStyles = SysGlob.NumberStyles.AllowDecimalPoint | SysGlob.NumberStyles.AllowExponent | SysGlob.NumberStyles.AllowLeadingSign;
		return double.TryParse( s, numberStyles, SysGlob.CultureInfo.InvariantCulture, out value );
	}

	private static readonly HashSet<Sys.Type> reportedTypes = new HashSet<Sys.Type>();

	// PEARL: Arrays in C# implement `IEnumerable` but provide no implementation for `Equals()`!
	//        This means that `object.Equals( array1, array2 )` will always return false, even if the arrays have identical contents!
	//        This is especially sinister since arrays are often treated as `IEnumerable`, so you may have two instances of `IEnumerable`
	//        which yield identical elements and yet the instances fail to return `true` when checked using `object.Equals()`.
	//        The standing advice is to use `a.SequenceEqual( b )` to compare `IEnumerable`, which is retarded, due to the following reasons:
	//          1. This will only work when you know the exact types of the objects being compared; it might suit application programmers who are perfectly
	//             accustomed writing copious amounts of mindless application-specific code to accomplish standard tasks, but it does not work when you are
	//             writing framework-level code, which operates on data without needing to know (nor wanting to know) the exact type of the data.
	//          2. This will not work when the `IEnumerable`s in turn contain other `IEnumerable`s (or arrays) because guess what `SequenceEqual()` uses
	//             internally to compare each pair of elements of the `IEnumerable`s? It uses `object.Equals()`, which miserably fails when comparing
	//             instances of `IEnumerable`! Again, this might be fine for application programmers who will happily write thousands of lines of
	//             application-specific code to compare application data having intimate knowledge of the structure of the data, but it does not work when
	//             writing framework-level code.
	//        This method fixes this insanity. It is meant to be used as a replacement for `object.Equals()` under all circumstances.
	public new static bool Equals( object? a, object? b )
	{
		if( ReferenceEquals( a, b ) )
			return true;
		if( a == null || b == null )
			return false;
		if( a is Legacy.IEnumerable enumerableA && b is Legacy.IEnumerable enumerableB )
			return legacyEnumerablesEqual( enumerableA, enumerableB );
		Sys.Type type = a.GetType();
		if( DebugMode && !overridesEqualsMethod( type ) )
		{
			lock( reportedTypes )
			{
				if( !reportedTypes.Contains( type ) )
				{
					Log.Error( $"Type {FrameworkHelpers.GetCSharpTypeName( type )} does not override object.Equals() !" );
					reportedTypes.Add( type );
				}
			}
		}
		return a.Equals( b );

		static bool overridesEqualsMethod( Sys.Type type )
		{
			SysReflect.MethodInfo equalsMethod = type.GetMethods( SysReflect.BindingFlags.Instance | SysReflect.BindingFlags.Public ) //
					.Single( m => m.Name == "Equals" && m.GetBaseDefinition().DeclaringType == typeof(object) );
			return equalsMethod.DeclaringType != typeof(object);
		}

		static bool legacyEnumerablesEqual( Legacy.IEnumerable a, Legacy.IEnumerable b )
		{
			Legacy.IEnumerator enumerator1 = a.GetEnumerator();
			Legacy.IEnumerator enumerator2 = b.GetEnumerator();
			try
			{
				while( enumerator1.MoveNext() )
				{
					if( !enumerator2.MoveNext() )
						return false;
					if( !Equals( enumerator1.Current, enumerator2.Current ) )
						return false;
				}
				if( enumerator2.MoveNext() )
					return false;
				return true;
			}
			finally
			{
				(enumerator1 as Sys.IDisposable)?.Dispose();
				(enumerator2 as Sys.IDisposable)?.Dispose();
			}
		}
	}

	// public static void ForEach<T>( Sys.Array array, Procedure<T, IReadOnlyList<int>> valueConsumer )
	// {
	// 	Assert( array.Rank > 1 );
	// 	foreach( (object value, IReadOnlyList<int> indices) in array.Enumerate() )
	// 		valueConsumer.Invoke( (T)value, indices );
	// }
	//
	// public static IEnumerable<(object, IReadOnlyList<int>)> Enumerate( Sys.Array array )
	// {
	// 	Assert( array.Rank > 1 );
	// 	IReadOnlyList<IEnumerable<int>> indexRanges       = Enumerable.Range( 0, array.Rank ).Select( array.IndexRange ).ToArray();
	// 	IEnumerable<IReadOnlyList<int>> indexCombinations = EnumerateCombinations( indexRanges );
	// 	return indexCombinations.Select( indices => (array.GetValue( indices.ToArray() ), indices) );
	// }
	//
	// public static IEnumerable<int> IndexRange( Sys.Array array, int dimension ) => Enumerable.Range( 0, array.GetLength( dimension ) - 1 );
	//
	// public static IEnumerable<IReadOnlyList<T>> EnumerateCombinations<T>( IReadOnlyList<IEnumerable<T>> ranges )
	// {
	// 	T[] values = new T[ranges.Count];
	// 	return recurse( 0 );
	//
	// 	IEnumerable<T[]> recurse( int depth )
	// 	{
	// 		foreach( T value in ranges[depth] )
	// 		{
	// 			values[depth] = value;
	// 			if( depth == ranges.Count - 1 )
	// 				yield return values;
	// 			else
	// 				foreach( var result in recurse( depth + 1 ) ) //NOTE: this might be inefficient, consider replacing with RecursiveSelect() below.
	// 					yield return result;
	// 		}
	// 	}
	// }
	//
	// public static IEnumerable<IReadOnlyList<T>> EnumerateCombinations2<T>( IReadOnlyList<IEnumerable<T>> ranges )
	// {
	// 	T[] values = new T[ranges.Count];
	//
	// 	IEnumerable<(T, int)> elements = ranges[0].RecursiveSelect( ( value, depth ) =>
	// 		{
	// 			values[depth] = value;
	// 			return ranges[depth];
	// 		} );
	// 	return elements.Where( ( (T value, int depth) tuple ) => tuple.depth == ranges.Count - 1 ).Select( ( (T value, int depth) tuple ) => values );
	// }
	//
	// public static IEnumerable<(T, int)> RecursiveSelect<T>( T source, Function<IEnumerable<T>, T, int> childSelector ) { return RecursiveSelect( childSelector( source, 0 ), childSelector ); }
	//
	// // From https://stackoverflow.com/a/30441479/773113
	// public static IEnumerable<(T, int)> RecursiveSelect<T>( IEnumerable<T> source, Function<IEnumerable<T>, T, int> childSelector )
	// {
	// 	var                         stack      = new Stack<IEnumerator<T>>();
	// 	IEnumerator<T>? enumerator = source.GetEnumerator();
	// 	try
	// 	{
	// 		while( true )
	// 		{
	// 			if( enumerator.MoveNext() )
	// 			{
	// 				T   element = enumerator.Current;
	// 				int depth   = stack.Count;
	// 				yield return (element, depth);
	// 				stack.Push( enumerator );
	// 				enumerator = childSelector( element, depth ).GetEnumerator();
	// 			}
	// 			else if( stack.Count > 0 )
	// 			{
	// 				enumerator.Dispose();
	// 				enumerator = stack.Pop();
	// 			}
	// 			else
	// 				yield break;
	// 		}
	// 	}
	// 	finally
	// 	{
	// 		enumerator.Dispose();
	// 		while( stack.Count > 0 ) // Clean up in case of an exception.
	// 		{
	// 			enumerator = stack.Pop();
	// 			enumerator.Dispose();
	// 		}
	// 	}
	// }
	//
	// public static void PopulateArray<T>( Sys.Array array, Function<T, IReadOnlyList<int>> valueProducer )
	// {
	// 	void action( T element, IReadOnlyList<int> indexArray )
	// 	{
	// 		T value = valueProducer.Invoke( indexArray );
	// 		array.SetValue( value, indexArray.ToArray() );
	// 	}
	//
	// 	array.ForEach<T>( action );
	// }

	public static int GetJaggedRank( Sys.Array jagged ) => GetJaggedRank( jagged.GetType() );

	public static int GetJaggedRank( Sys.Type type )
	{
		Assert( type.IsArray ); //guaranteed to succeed.
		Assert( type.GetArrayRank() == 1 ); //the array must be jagged, not multi-dimensional.
		for( int i = 1;; i++ )
		{
			type = type.GetElementType()!;
			if( !type.IsArray || type.GetArrayRank() != 1 )
				return i;
		}
	}

	public static Sys.Type GetJaggedElementType( Sys.Array jagged, int rank ) => GetJaggedElementType( jagged.GetType(), rank );

	public static Sys.Type GetJaggedElementType( Sys.Type type, int rank )
	{
		Assert( type.IsArray ); //guaranteed to succeed.
		Assert( type.GetArrayRank() == 1 ); //the array must be jagged, not multi-dimensional.
		Assert( rank == GetJaggedRank( type ) ); //the given rank must be the correct rank.
		for( ;; )
		{
			type = type.GetElementType()!;
			rank--;
			if( rank == 0 )
				break;
		}
		return type;
	}

	public static int GetJaggedLength( Sys.Array jagged, int dimension )
	{
		Assert( jagged.Rank == 1 ); //the array must be jagged, not multi-dimensional.
		Assert( dimension >= 0 && dimension < GetJaggedRank( jagged ) ); //the dimension must be between 0 and the rank of the jagged array.
		if( jagged.GetLength( 0 ) > 0 )
		{
			for( ; dimension > 0; dimension-- )
			{
				jagged = (Sys.Array)NotNull( jagged.GetValue( 0 ) );
				Assert( jagged.Rank == 1 ); //nested array must also be a jagged array
			}
		}
		return jagged.GetLength( 0 );
	}

	public static int[] GetJaggedLengths( Sys.Array jagged, int rank )
	{
		Assert( jagged.Rank == 1 ); //the array must be jagged, not multi-dimensional.
		Assert( rank == GetJaggedRank( jagged ) ); //the given rank must be the correct rank.
		int[] lengths = new int[rank];
		for( int dimension = 0; dimension < rank; dimension++ )
			lengths[dimension] = GetJaggedLength( jagged, dimension );
		return lengths;
	}

	public static bool JaggedIsNormalAssertion( Sys.Array jagged )
	{
		int rank = GetJaggedRank( jagged );
		recurse( 0, jagged );
		return true;

		int recurse( int dimension, Sys.Array jagged )
		{
			Assert( jagged.GetLowerBound( 0 ) == 0 );
			int length = jagged.GetLength( 0 );
			if( length == 0 )
				return length;
			if( dimension == rank - 1 )
				return length;
			Sys.Array firstJaggedChild = (Sys.Array)NotNull( jagged.GetValue( 0 ) );
			int firstChildLength = recurse( dimension + 1, firstJaggedChild );
			for( int index = 1; index < length; index++ )
			{
				Sys.Array anotherJaggedChild = (Sys.Array)NotNull( jagged.GetValue( index ) );
				int anotherChildLength = recurse( dimension + 1, anotherJaggedChild );
				Assert( firstChildLength == anotherChildLength );
			}
			return length;
		}
	}

	public static Sys.Array JaggedFromMultiDimensional( Sys.Array multiDimensional )
	{
		Assert( multiDimensional.Rank > 1 ); //this is not a multi-dimensional array
		Assert( IsZeroBasedAssertion( multiDimensional ) ); //this is not a zero-based array
		int[] indices = new int[multiDimensional.Rank];
		return recurse( 0 );

		Sys.Array recurse( int dimension )
		{
			Sys.Type jaggedArrayElementType = makeJaggedArrayElementType( multiDimensional.GetType().GetElementType()!, multiDimensional.Rank - dimension - 1 );
			int length = multiDimensional.GetLength( dimension );
			Sys.Array jagged = Sys.Array.CreateInstance( jaggedArrayElementType, length );
			for( int index = 0; index < length; index++ )
			{
				indices[dimension] = index;
				jagged.SetValue( dimension == multiDimensional.Rank - 1 ? multiDimensional.GetValue( indices ) : recurse( dimension + 1 ), index );
			}
			return jagged;

			static Sys.Type makeJaggedArrayElementType( Sys.Type elementType, int rank )
			{
				Assert( rank >= 0 );
				return rank == 0 ? elementType : makeJaggedArrayElementType( elementType, rank - 1 ).MakeArrayType();
			}
		}
	}

	public static Sys.Array MultiDimensionalFromJagged( Sys.Array jagged )
	{
		Assert( jagged.Rank == 1 ); //this is not a jagged array!
		Assert( GetJaggedRank( jagged ) > 1 ); //jagged array has one dimension, so it is already continuous.
		int rank = GetJaggedRank( jagged );
		Assert( JaggedIsNormalAssertion( jagged ) );
		Sys.Type elementType = GetJaggedElementType( jagged, rank );
		Sys.Array multiDimensional = Sys.Array.CreateInstance( elementType, GetJaggedLengths( jagged, rank ) );
		int[] indices = new int[multiDimensional.Rank];
		recurse( 0, jagged );
		return multiDimensional;

		// ReSharper disable once VariableHidesOuterVariable
		void recurse( int dimension, Sys.Array jagged )
		{
			int length = jagged.GetLength( 0 );
			Assert( length == multiDimensional.GetLength( dimension ) );
			for( int index = 0; index < length; index++ )
			{
				indices[dimension] = index;
				if( dimension == multiDimensional.Rank - 1 )
					multiDimensional.SetValue( jagged.GetValue( index ), indices );
				else
					recurse( dimension + 1, (Sys.Array)NotNull( jagged.GetValue( index ) ) );
			}
		}
	}

	public static bool IsZeroBasedAssertion( Sys.Array array )
	{
		int rank = array.Rank;
		for( int dimension = 0; dimension < rank; dimension++ )
			Assert( array.GetLowerBound( dimension ) == 0 ); //non-zero lower bounds are not supported. (They are easy to support, but highly unlikely to ever be used.)
		return true;
	}

	public static void CopyTo<T>( ICollection<T> collection, T[] array, int arrayIndex )
	{
		int maxLen = Math.Max( array.Length, arrayIndex + collection.Count );
		using( IEnumerator<T> enumerator = collection.GetEnumerator() )
			for( int i = arrayIndex; enumerator.MoveNext() && i < maxLen; i++ )
				array[i] = enumerator.Current;
	}

	// PEARL: System.BitConverter has an 'IsLittleEndian' field, which will tell you the endianness of the system,
	//        but it does not offer any means of selecting the endianness of the conversions!
	//        For that, you need separate calls to IPAddress.HostToNetworkOrder() and IPAddress.NetworkToHostOrder(),
	//        which are retarded, because they allow the existence of (no matter how temporary) bogus integer values.
	//        We hide all this insanity behind this pair of functions.
	public static void WriteInt( SysIo.Stream stream, int value )
	{
		int networkOrderValue = SysNet.IPAddress.HostToNetworkOrder( value );
		byte[] bytes = Sys.BitConverter.GetBytes( networkOrderValue );
		Assert( bytes.Length == 4 );
		stream.Write( bytes, 0, bytes.Length );
	}

	// See WriteInt() above.
	public static int ReadInt( SysIo.Stream stream )
	{
		byte[] bytes = new byte[4];
		int n = stream.Read( bytes, 0, bytes.Length );
		if( n == 0 )
			throw new SysIo.EndOfStreamException();
		if( n != bytes.Length ) //NOTE: there is a chance that this may, under normal circumstances, return less than n; if it happens, add code to handle it.
			throw new SysIo.EndOfStreamException( $"Expected {bytes.Length} bytes, got {n}" );
		int networkOrderValue = Sys.BitConverter.ToInt32( bytes, 0 );
		return SysNet.IPAddress.NetworkToHostOrder( networkOrderValue );
	}

	public static bool IsReferenceTypeOrNullableValueType( Sys.Type type )
	{
		if( !type.IsValueType )
			return true;
		return IsNullableValueType( type );
	}

	public static bool IsNullableValueType( Sys.Type type )
	{
		return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Sys.Nullable<>);
	}

	public static Sys.Type GetNonNullableValueType( Sys.Type type )
	{
		Assert( IsNullableValueType( type ) );
		return type.GenericTypeArguments[0];
	}

	// PEARL: When trying to step into List<T>.Equals() with the debugger,
	//        there appears to be no source code available, (or not easily available,)
	//        and for some reason Visual Studio refuses to just disassemble it and step into it,
	//        and I cannot even find the type in the object browser,
	//        so in order to find out in exactly what ways two lists are not equal, I have no option but
	//        to implement and use this method so that I can single-step through it.
	// PEARL: There appears to be no built-in method in DotNet for comparing the contents of two arrays.
	//        The List<T>.Equals() method cannot be used, because it stupidly works on List<T>, not on IReadOnlyList<T>.
	//        People on StackOverflow suggest using the SequenceEqual method of linq, but I do not expect this to perform well at all.
	//        Luckily, the following method can also be used to compare the contents of two arrays.
	public static bool ListEquals<T>( IReadOnlyList<T> a, IReadOnlyList<T> b )
	{
		if( !DebugMode )
			return Equals( a, b );
		int n = Math.Min( a.Count, b.Count );
		for( int i = 0; i < n; i++ )
		{
			if( !Equals( a[i], b[i] ) )
				return false; // <-- place breakpoint here.
		}
		return a.Count == b.Count;
	}

	public static void ExecuteAndWait( DirectoryPath workingDirectory, FilePath executable, IEnumerable<string> arguments )
	{
		int exitCode = ExecuteAndWaitForExitCode( workingDirectory, executable, arguments );
		if( exitCode != 0 )
			throw new Sys.Exception( $"The command \"{executable}\" in '{workingDirectory.Path}' returned exit code {exitCode}" );
	}

	public static int ExecuteAndWaitForExitCode( DirectoryPath workingDirectory, FilePath executable, IEnumerable<string> arguments )
	{
		var process = Execute( workingDirectory, executable, arguments );
		process.WaitForExit();
		return process.ExitCode;
	}

	public static SysDiag.Process Execute( DirectoryPath workingDirectory, FilePath executable, params string[] arguments )
	{
		return Execute( workingDirectory, executable, (IEnumerable<string>)arguments );
	}

	public static SysDiag.Process Execute( DirectoryPath workingDirectory, FilePath executable, IEnumerable<string> arguments )
	{
		var process = new SysDiag.Process();
		process.StartInfo.FileName = executable.FullName;
		process.StartInfo.Arguments = arguments.MakeString( " " );
		process.StartInfo.UseShellExecute = false; //PEARL: by "shell" here they mean the Windows Explorer, not cmd.exe
		process.StartInfo.WindowStyle = SysDiag.ProcessWindowStyle.Normal;
		process.StartInfo.CreateNoWindow = false;
		process.StartInfo.WorkingDirectory = workingDirectory.Path;
		try
		{
			process.Start();
		}
		catch( Sys.Exception exception )
		{
			throw new Sys.Exception( $"Failed to execute command '{executable}' in '{workingDirectory}'", exception );
		}
		return process;
	}

	public static bool NamedPipeServerIsListening( string serverName, string pipeName )
	{
		return SysIo.File.Exists( $@"\\{serverName}\pipe\{pipeName}" ); //see https://stackoverflow.com/a/63739027/773113
	}

	public static IReadOnlyDictionary<K, V> DictionaryFromValues<K, V>( IEnumerable<V> valuesEnumerable, Function<K, V> keyExtractor )
			where K : notnull
	{
		IList<V> values = valuesEnumerable.ToList();
		Dictionary<K, V> dictionary = new Dictionary<K, V>( values.Count );
		foreach( V value in values )
		{
			K key = keyExtractor.Invoke( value );
			dictionary.Add( key, value );
		}
		return dictionary;
	}

	public static E EnumFromUInt<E>( uint value )
			where E : struct, Sys.Enum
	{
		int intValue = (int)value;
		E enumValue = (E)(object)intValue; //see https://stackoverflow.com/a/51025027/773113
		Assert( IsValidEnumValueAssertion( enumValue ) );
		return enumValue;
	}

	public static IReadOnlyList<E> GetEnumValues<E>()
	{
		Sys.Type enumType = typeof(E);
		Assert( enumType.IsEnum );
		return (E[])Sys.Enum.GetValues( enumType ); //see https://stackoverflow.com/a/105402/773113
	}

	public static bool IsValidEnumValueAssertion<E>( E enumValue )
			where E : struct, Sys.Enum
	{
		Assert( IsValidEnumValue( enumValue ) );
		return true;
	}

	public static bool IsValidEnumValue<E>( E enumValue )
			where E : struct, Sys.Enum
	{
		foreach( E value in GetEnumValues<E>() )
			if( Equals( value, enumValue ) )
				return true;
		return true;
	}

	// See https://stackoverflow.com/a/1987721/773113
	// this method will round and then append zeros if needed.
	// i.e. if you round .002 to two significant figures, the resulting number should be .0020.
	public static string ToString( double value, int significantDigits )
	{
		var currentInfo = SysGlob.CultureInfo.CurrentCulture.NumberFormat;

		if( double.IsNaN( value ) )
			return currentInfo.NaNSymbol;

		if( double.IsPositiveInfinity( value ) )
			return currentInfo.PositiveInfinitySymbol;

		if( double.IsNegativeInfinity( value ) )
			return currentInfo.NegativeInfinitySymbol;

		var roundedValue = round_significant_digits( value, significantDigits, out _ );

		// when rounding causes a cascading round affecting digits of greater significance, 
		// need to re-round to get a correct rounding position afterwards
		// this fixes a bug where rounding 9.96 to 2 figures yields 10.0 instead of 10
		round_significant_digits( roundedValue, significantDigits, out int roundingPosition );

		if( Math.Abs( roundingPosition ) > 9 )
		{
			// use exponential notation format
			// ReSharper disable FormatStringProblem
			return string.Format( currentInfo, "{0:E" + (significantDigits - 1) + "}", roundedValue );
			// ReSharper restore FormatStringProblem
		}

		// string.format is only needed with decimal numbers (whole numbers won't need to be padded with zeros to the right.)
		// ReSharper disable FormatStringProblem
		return roundingPosition > 0 ? string.Format( currentInfo, "{0:F" + roundingPosition + "}", roundedValue ) : roundedValue.ToString( currentInfo );
		// ReSharper restore FormatStringProblem
	}

	private static double round_significant_digits( double value, int significantDigits, out int roundingPosition )
	{
		// this method will return a rounded double value at a number of significant figures.
		// the sigFigures parameter must be between 0 and 15, exclusive.

		roundingPosition = 0;

		if( DoubleEquals( value, 0d ) )
		{
			roundingPosition = significantDigits - 1;
			return 0d;
		}

		if( double.IsNaN( value ) )
			return double.NaN;

		if( double.IsPositiveInfinity( value ) )
			return double.PositiveInfinity;

		if( double.IsNegativeInfinity( value ) )
			return double.NegativeInfinity;

		if( significantDigits < 1 || significantDigits > 15 )
			throw new Sys.ArgumentOutOfRangeException( nameof(significantDigits), value, "The significantDigits argument must be between 1 and 15." );

		// The resulting rounding position will be negative for rounding at whole numbers, and positive for decimal places.
		roundingPosition = significantDigits - 1 - (int)Math.Floor( Math.Log10( Math.Abs( value ) ) );

		// try to use a rounding position directly, if no scale is needed.
		// this is because the scale multiplication after the rounding can introduce error, although 
		// this only happens when you're dealing with really tiny numbers, i.e 9.9e-14.
		if( roundingPosition > 0 && roundingPosition < 16 )
			return Math.Round( value, roundingPosition, Sys.MidpointRounding.AwayFromZero );

		// Shouldn't get here unless we need to scale it.
		// Set the scaling value, for rounding whole numbers or decimals past 15 places
		var scale = Math.Pow( 10, Math.Ceiling( Math.Log10( Math.Abs( value ) ) ) );

		return Math.Round( value / scale, significantDigits, Sys.MidpointRounding.AwayFromZero ) * scale;
	}

	public static double Round( double value, int significantDigits )
	{
		return round_significant_digits( value, significantDigits, out _ );
	}

	///Tries to find an argument by prefix in the given list of arguments, extracts it from the list, and returns the remainder after the prefix. Returns `null` if not found.
	public static string? TryExtractArgumentByPrefix( List<string> arguments, string argumentPrefix )
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
	public static string? TryExtractOption( List<string> arguments, string argumentName )
	{
		return TryExtractArgumentByPrefix( arguments, argumentName + "=" );
	}

	///Finds an option of the form name=value in the given list of arguments, extracts it from the list, and returns the value. Returns the supplied default if not found, fails if the default is `null`.
	public static string ExtractOption( List<string> arguments, string argumentName, string? defaultValue = null )
	{
		string? value = TryExtractOption( arguments, argumentName );
		if( value == null )
		{
			if( defaultValue == null )
				throw new Sys.Exception( $"Expected '{argumentName}='" );
			return defaultValue;
		}
		return value;
	}

	///Finds a switch of the form name[=true|=false] in the given list of arguments, extracts it from the list, and
	///returns the value. Returns `false` if the name not found; returns `true` if the name is found but the value is
	///not supplied.
	public static bool ExtractSwitch( List<string> args, string prefix )
	{
		if( args.Remove( prefix ) )
			return true;
		string? argument = TryExtractOption( args, prefix );
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

	public static int EnumerableHashCode<T>( IEnumerable<T> enumerable )
	{
		Sys.HashCode hashCode = new Sys.HashCode();
		foreach( var element in enumerable )
			hashCode.Add( element );
		return hashCode.ToHashCode();
	}

	public static IEnumerable<T> EnumerableFromArray<T>( Sys.Array target )
	{
		foreach( var item in target )
			yield return (T)item;
	}

	public static IEnumerable<T> EnumerableFromArray<T>( T[,] target )
	{
		foreach( var item in target )
			yield return item;
	}

	public static int ArrayHashCode<T>( T[] array ) => EnumerableHashCode( array );
	public static int ArrayHashCode<T>( T[,] array ) => EnumerableHashCode( EnumerableFromArray( array ) );
	public static int ArrayHashCode<T>( Sys.Array array ) => EnumerableHashCode( EnumerableFromArray<T>( array ) );

	public static void AppendEnumerable<T>( SysText.StringBuilder self, IEnumerable<T> enumerable, string prefix, string delimiter, string suffix, string ifEmpty )
	{
		bool first = true;
		IList<T> selfAsList = enumerable.ToImmutableList();
		foreach( T element in selfAsList )
		{
			if( first )
			{
				self.Append( prefix );
				first = false;
			}
			else
				self.Append( delimiter );
			self.Append( element );
		}
		self.Append( first ? ifEmpty : suffix );
	}

	public static T TryExceptDefault<T>( Function<T> function, Function<T> defaultResult, string logMessageOnError = "" )
	{
		try
		{
			return function.Invoke();
		}
		catch( Sys.Exception exception )
		{
			if( string.IsNullOrEmpty( logMessageOnError ) )
				Log.Debug( $"Returned default due to exception: {exception.Message}" );
			else
				Log.Debug( logMessageOnError );
			return defaultResult.Invoke();
		}
	}

	private sealed class IdentityEqualityComparer<T> : IEqualityComparer<T>
			where T : class
	{
		public int GetHashCode( T value )
		{
			return SysComp.RuntimeHelpers.GetHashCode( value );
		}

		public bool Equals( T? left, T? right )
		{
			return ReferenceEquals( left, right ); //I am not sure whether `==` would suffice here, (what if it is overloaded?) so better be safe and call `ReferenceEquals`.
		}
	}

	public static IDictionary<K, V> NewIdentityDictionary<K, V>()
			where K : class
	{
		return new Dictionary<K, V>( new IdentityEqualityComparer<K>() );
	}

	public static ISet<T> NewIdentitySet<T>()
			where T : class
	{
		return new HashSet<T>( new IdentityEqualityComparer<T>() );
	}

	public static string StripForbiddenPathNameCharacters( string text )
	{
		if( !ContainsForbiddenPathNameCharacters( text ) )
			return text;
		var stringBuilder = new SysText.StringBuilder();
		foreach( char c in text )
			if( !IsForbiddenPathNameCharacter( c ) )
				stringBuilder.Append( c );
		return stringBuilder.ToString();
	}

	public static bool ContainsForbiddenPathNameCharacters( string text )
	{
		foreach( char c in text )
			if( IsForbiddenPathNameCharacter( c ) )
				return true;
		return false;
	}

	public static bool IsForbiddenPathNameCharacter( char c )
	{
		if( @"<>:""/\|?*".Contains( c ) ) //these characters are explicitly forbidden.
			return true;
		if( c < 32 ) //we also forbid control characters.
			return true;
		return false;
	}

	///<summary>Converts a <see cref="Sys.TimeSpan"/> to a number usable by various DotNet functions that require a timeout expressed as an integer number of milliseconds.</summary>
	///<remarks>If the TimeSpan is equal to MaxValue, then -1 is returned, to select an infinite timeout.</remarks>
	public static int ToMilliseconds( Sys.TimeSpan self )
	{
		if( self == Sys.TimeSpan.MaxValue )
			return -1; //infinity
		double milliseconds = self.TotalMilliseconds;
		Assert( milliseconds >= 0.0 && milliseconds < int.MaxValue );
		return (int)milliseconds;
	}

	public static byte[] GetResourceBytes( string resourceName, Sys.Type? locatorType )
	{
		SysReflect.Assembly locatorAssembly;
		string fullResourceName;
		if( locatorType == null )
		{
			locatorAssembly = NotNull( SysReflect.Assembly.GetEntryAssembly() );
			fullResourceName = resourceName;
		}
		else
		{
			locatorAssembly = locatorType.Assembly;
			fullResourceName = locatorType.Namespace == null ? resourceName : locatorType.Namespace + "." + resourceName;
		}

		SysIo.Stream? stream = locatorAssembly.GetManifestResourceStream( fullResourceName );
		if( stream == null )
		{
			string availableResources = locatorAssembly.GetManifestResourceNames().MakeString( "'", "', '", "'", "none" );
			throw new Sys.Exception( $"resource '{resourceName}' not found.  Locator type: '{locatorType?.FullName}'; Assembly: '{locatorAssembly}'; Available resources: {availableResources}" );
		}
		using( stream )
		{
			return ReadAll( stream );
		}
	}

	public static SysIo.Stream GetResource( string resourceName, Sys.Type? locatorType )
	{
		(SysReflect.Assembly assembly, string fullResourceName) = LocateResource( resourceName, locatorType );
		return GetResource( assembly, fullResourceName );
	}

	public static SysIo.Stream GetResource( SysReflect.Assembly assembly, string fullResourceName )
	{
		SysIo.Stream? stream = TryGetResource( assembly, fullResourceName );
		if( stream == null )
		{
			string availableResources = assembly.GetManifestResourceNames().MakeString( "'", "', '", "'", "none" );
			throw new Sys.Exception( $"resource '{fullResourceName}' not found.  Assembly: '{assembly}'; Available resources: {availableResources}" );
		}
		return stream;
	}

	public static SysIo.Stream? TryGetResource( SysReflect.Assembly assembly, string fullResourceName )
	{
		return assembly.GetManifestResourceStream( fullResourceName );
	}

	public static (SysReflect.Assembly assembly, string fullResourceName) LocateResource( string resourceName, Sys.Type? locatorType )
	{
		return locatorType == null ? (NotNull( SysReflect.Assembly.GetEntryAssembly() ), resourceName) : (locatorType.Assembly, fixResourceName( resourceName, locatorType ));

		static string fixResourceName( string s, Sys.Type type ) => type.Namespace == null ? s : type.Namespace + "." + s;
	}
}
