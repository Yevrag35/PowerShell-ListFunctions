namespace ListFunctions.Extensions;

/// <summary>
/// Provides extension methods that convert an item of a PowerShell output collection, such as the result of
/// <see cref="ScriptBlock.Invoke(object[])"/>, to a typed value.
/// </summary>
/// <remarks>
/// The methods never throw for a missing item or a failed conversion. They return a default value instead.
/// </remarks>
public static class PSVariableCollectionExtensions
{
	/// <summary>
	/// Converts the first item of the collection to <typeparamref name="T"/>, or returns a default value if it can't.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method unwraps every <see cref="PSObject"/> layer of the first item before it passes the item to
	/// <paramref name="convert"/>. It ignores every item after the first.
	/// </para>
	/// <para>
	/// The method returns <paramref name="defaultIfNull"/> without calling <paramref name="convert"/> when
	/// <paramref name="collection"/> is <see langword="null"/> or empty, when its first item is <see langword="null"/>,
	/// or when the unwrapped item is <see langword="null"/>.
	/// </para>
	/// <para>
	/// If <paramref name="convert"/> throws, the method writes the exception's message to the debug output and returns
	/// <paramref name="defaultIfNull"/>.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">The type to convert the item to.</typeparam>
	/// <param name="collection">The collection to read. This value can be <see langword="null"/>.</param>
	/// <param name="convert">
	/// The function that converts the unwrapped item to <typeparamref name="T"/>. It never receives
	/// <see langword="null"/>. This value must not be <see langword="null"/>; the method treats a <see langword="null"/> function as a failed conversion.
	/// </param>
	/// <param name="defaultIfNull">The value to return when the method can't convert an item. The default is <see langword="default"/>.</param>
	/// <returns>
	/// The value that <paramref name="convert"/> returns for the first item; otherwise, <paramref name="defaultIfNull"/>.
	/// </returns>
	[return: NotNullIfNotNull(nameof(defaultIfNull))]
	public static T GetFirstValue<T>(this Collection<PSObject>? collection, Func<object, T> convert, T defaultIfNull = default!)
	{
		if (collection is null || collection.Count == 0 || collection[0] is not PSObject pso || !pso.TryGetBaseObject(out object? o))
		{
			return defaultIfNull;
		}

		try
		{
			return convert(o);
		}
		catch (Exception e)
		{
			Debug.WriteLine(e.Message);
			return defaultIfNull;
		}
	}

	/// <summary>
	/// Converts the last item of the collection to <typeparamref name="T"/>, or returns <see langword="default"/> if it can't.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Unlike <see cref="GetFirstValue{T}(Collection{PSObject}, Func{object, T}, T)"/>, this method unwraps at most two
	/// <see cref="PSObject"/> layers of the last item, through <see cref="PSObject.ImmediateBaseObject"/>, before it
	/// passes the item to <paramref name="convert"/>. An item wrapped in more layers reaches <paramref name="convert"/> as a
	/// <see cref="PSObject"/>.
	/// </para>
	/// <para>
	/// The method returns <see langword="default"/> without calling <paramref name="convert"/> when
	/// <paramref name="collection"/> is <see langword="null"/> or empty, or when its last item or that item's immediate base
	/// object is <see langword="null"/>.
	/// </para>
	/// <para>
	/// If <paramref name="convert"/> throws, the method discards the exception and returns <see langword="default"/>.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">The type to convert the item to.</typeparam>
	/// <param name="collection">The collection to read. This value can be <see langword="null"/>.</param>
	/// <param name="convert">
	/// The function that converts the unwrapped item to <typeparamref name="T"/>. It never receives <see langword="null"/>.
	/// This value must not be <see langword="null"/>; the method treats a <see langword="null"/> function as a failed conversion.
	/// </param>
	/// <returns>
	/// The value that <paramref name="convert"/> returns for the last item; otherwise, <see langword="default"/>.
	/// </returns>
	[return: MaybeNull]
	public static T GetLastValue<T>(this Collection<PSObject>? collection, Func<object, T> convert)
	{
		if (collection is null || collection.Count == 0)
			return default;

		object? last = collection[collection.Count - 1]?.ImmediateBaseObject;
		if (last is null)
			return default;

		try
		{
			return convert(PSObject.AsPSObject(last).ImmediateBaseObject);
		}
		catch
		{
			return default;
		}
	}
}
