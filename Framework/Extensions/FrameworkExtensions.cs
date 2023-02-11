namespace Framework.Extensions;

using System.Collections.Generic;
using System.Collections.Immutable;
using SysText = SysText;
using static Statics;

public static class FrameworkExtensions
{
	///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// IEnumerable

	public static string MakeString<T>( this IEnumerable<T> self, string delimiter = "" ) => self.MakeString( "", delimiter, "", "" );

	public static string MakeString<T>( this IEnumerable<T> self, string prefix, string delimiter, string suffix, string if_empty )
	{
		var string_builder = new SysText.StringBuilder();
		string_builder.AppendEnumerable( self, prefix, delimiter, suffix, if_empty );
		return string_builder.ToString();
	}

	///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// IList

	public static T ExtractAt<T>( this IList<T> self, int index )
	{
		T result = self[index];
		self.RemoveAt( index );
		return result;
	}

	//PEARL: the remove-item-from-list method of DotNet is not really a "remove" method, it is actually a "try-remove" method, because it returns a
	//boolean to indicate success or failure. So, if we want a real "remove" function which will actually fail on failure, (duh!) we have to introduce it
	//ourselves.  Unfortunately, since the name `Remove` is taken, we have to give the new function a different name, (I chose `DoRemove`,) so we still
	//have to remember to invoke `DoRemove()` instead of `Remove()`.
	public static void DoRemove<T>( this IList<T> self, T item )
	{
		bool ok = self.Remove( item );
		Assert( ok );
	}

	///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// IDictionary

	//PEARL: the remove-item-from-dictionary method of DotNet is not really a "remove" method, it is actually a "try-remove" method, because it returns a
	//boolean to indicate success or failure. So, if we want a real "remove" function which will actually fail on failure, (duh!) we have to introduce it
	//ourselves.  Unfortunately, since the name `Remove` is taken, we have to give the new function a different name, (I chose `DoRemove`,) so we still
	//have to remember to invoke `DoRemove()` instead of `Remove()`.
	public static void DoRemove<K, V>( this IDictionary<K, V> self, K key )
	{
		bool ok = self.Remove( key );
		Assert( ok );
	}

	///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// StringBuilder

	public static void AppendEnumerable<T>( this SysText.StringBuilder self, IEnumerable<T> enumerable, string prefix, string delimiter, string suffix, string if_empty )
	{
		bool first = true;
		IList<T> self_as_list = enumerable.ToImmutableList();
		foreach( T element in self_as_list )
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
		self.Append( first ? if_empty : suffix );
	}
}
