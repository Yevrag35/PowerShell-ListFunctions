namespace ListFunctions.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="Dictionary{TKey, TValue}"/> class.
/// </summary>
/// <remarks>
/// On .NET 5 and later, this class has no members, because <see cref="Dictionary{TKey, TValue}"/> provides
/// the same methods itself.
/// </remarks>
public static class DictionaryExtensions
{
#if !NET5_0_OR_GREATER
	/// <summary>
	/// Adds a key/value pair to the dictionary if the key does not already exist.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This method backfills the instance <c>TryAdd</c> method that <see cref="Dictionary{TKey, TValue}"/>
	/// gains in .NET Core 2.0 and later. It exists only in the .NET Standard 2.0 build.
	/// </para>
	/// <para>
	/// The method looks up <paramref name="key"/> twice when it adds the pair: once to check for it and once to add it.
	/// The check and the add are not atomic, so the method is not thread-safe.
	/// </para>
	/// <para>
	/// The <c>notnull</c> constraint on <typeparamref name="TKey"/> only produces a nullable warning. The method still
	/// checks <paramref name="key"/> for <see langword="null"/> at run time.
	/// </para>
	/// </remarks>
	/// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
	/// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
	/// <param name="dictionary">The dictionary to add the key/value pair to. This value must not be <see langword="null"/>.</param>
	/// <param name="key">The key of the element to add. This value must not be <see langword="null"/>.</param>
	/// <param name="value">The value of the element to add. This value can be <see langword="null"/> for reference types.</param>
	/// <returns>
	/// <see langword="true"/> if the key/value pair was added to the dictionary; <see langword="false"/> if
	/// <paramref name="key"/> already exists, in which case the existing value stays unchanged.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="dictionary"/> or <paramref name="key"/> is null.</exception>
	public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value) where TKey : notnull
	{
		Guard.NotNull(dictionary, nameof(dictionary));
		Guard.NotNull(key, nameof(key));

		bool added = false;
		if (!dictionary.ContainsKey(key))
		{
			dictionary.Add(key, value);
			added = true;
		}

		return added;
	}
#endif
}
