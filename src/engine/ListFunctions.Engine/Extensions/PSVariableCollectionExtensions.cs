namespace ListFunctions.Extensions;

/// <summary>
/// Provides an extension method that converts the first item of a PowerShell output collection, such as the result of
/// <see cref="ScriptBlock.Invoke(object[])"/>, to a typed value.
/// </summary>
/// <remarks>
/// The method never throws for a missing item or a failed conversion. It returns a default value instead.
/// </remarks>
internal static class PSVariableCollectionExtensions
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
}
