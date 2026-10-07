using ZLinq;

namespace ListFunctions.Internal;

/// <summary>
/// Provides shared, immutable empty collections.
/// </summary>
/// <remarks>
/// Each method returns the same cached instance on every call for a given set of type arguments, so the methods do not
/// allocate. The returned collections are thread-safe.
/// </remarks>
internal static class Empty
{
	/// <summary>
	/// Returns an empty read-only set.
	/// </summary>
	/// <remarks>
	/// The method is internal because the <c>netstandard2.0</c> build's <see cref="IReadOnlySet{T}"/> is an internal
	/// polyfill.
	/// </remarks>
	/// <typeparam name="T">The element type of the set.</typeparam>
	/// <returns>A cached <see cref="IReadOnlySet{T}"/> that contains no elements.</returns>
	internal static IReadOnlySet<T> Set<T>() => EmptyHolder<object, T>.Default;
	/// <summary>
	/// Returns an empty read-only dictionary.
	/// </summary>
	/// <remarks>
	/// Looking up any key through the indexer throws a <see cref="KeyNotFoundException"/>.
	/// </remarks>
	/// <typeparam name="TKey">The key type of the dictionary. Keys can't be <see langword="null"/>.</typeparam>
	/// <typeparam name="TValue">The value type of the dictionary.</typeparam>
	/// <returns>A cached <see cref="IReadOnlyDictionary{TKey, TValue}"/> that contains no entries.</returns>
	public static IReadOnlyDictionary<TKey, TValue> Dictionary<TKey, TValue>() where TKey : notnull => EmptyHolder<TKey, TValue>.Default;

	/// <summary>
	/// Holds the cached empty collection for one pair of type arguments.
	/// </summary>
	/// <remarks>
	/// The runtime initializes the field once per closed generic type, so the instance is created lazily and without locking.
	/// </remarks>
	/// <typeparam name="TKey">The key type of the empty dictionary.</typeparam>
	/// <typeparam name="TValue">The element type of the empty set, or the value type of the empty dictionary.</typeparam>
	private static class EmptyHolder<TKey, TValue> where TKey : notnull
	{
		/// <summary>
		/// The shared empty collection.
		/// </summary>
		public static readonly ReadOnlyEmpty<TKey, TValue> Default = new();
	}
}
/// <summary>
/// Represents a collection with no elements that serves as an empty read-only list, set, and dictionary at once.
/// </summary>
/// <remarks>
/// <para>
/// The type has no state, so a single instance per closed generic type can be shared freely. It is immutable and
/// thread-safe.
/// </para>
/// <para>
/// The <see cref="IReadOnlyList{T}"/> indexer returns <see langword="default"/> for any index instead of throwing an
/// <see cref="ArgumentOutOfRangeException"/>.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The key type when the instance is used as a dictionary. Keys can't be <see langword="null"/>.</typeparam>
/// <typeparam name="TValue">The element type when the instance is used as a list or set, or the value type when it is used as a dictionary.</typeparam>
[StructLayout(LayoutKind.Sequential)]
internal sealed class ReadOnlyEmpty<TKey, TValue> : IReadOnlyList<TValue>, IReadOnlySet<TValue>, IReadOnlyDictionary<TKey, TValue>
	where TKey : notnull
{
	/// <summary>
	/// Throws, because the dictionary contains no keys.
	/// </summary>
	/// <param name="key">The key to look up. This value must not be <see langword="null"/>.</param>
	/// <value>This indexer never returns a value.</value>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
	/// <exception cref="KeyNotFoundException">Thrown for every key that is not null.</exception>
	TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key]
	{
		[DoesNotReturn]
		get
		{
			ArgumentNullException.ThrowIfNull(key);
			throw new KeyNotFoundException();
		}
	}
	/// <summary>
	/// Gets the default value of <typeparamref name="TValue"/> for any index.
	/// </summary>
	/// <remarks>
	/// The index is not validated, so this indexer does not throw an <see cref="ArgumentOutOfRangeException"/> the way
	/// <see cref="IReadOnlyList{T}"/> implementations usually do.
	/// </remarks>
	/// <param name="index">The index to read. It is ignored.</param>
	/// <value>The <see langword="default"/> value of <typeparamref name="TValue"/>.</value>
	TValue IReadOnlyList<TValue>.this[int index] => default!;

	/// <summary>
	/// Gets the number of elements in the collection.
	/// </summary>
	/// <value>Always <c>0</c>.</value>
	public int Count => 0;
	/// <summary>
	/// Gets an empty sequence of keys.
	/// </summary>
	/// <value>A cached empty array of <typeparamref name="TKey"/>.</value>
	IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Array.Empty<TKey>();
	/// <summary>
	/// Gets an empty sequence of values.
	/// </summary>
	/// <value>A cached empty array of <typeparamref name="TValue"/>.</value>
	IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Array.Empty<TValue>();

	/// <summary>
	/// Initializes a new <see cref="ReadOnlyEmpty{TKey, TValue}"/> instance.
	/// </summary>
	/// <remarks>
	/// Use the cached instances from <see cref="Empty.Set{T}"/> and <see cref="Empty.Dictionary{TKey, TValue}"/> instead of
	/// creating new ones.
	/// </remarks>
	internal ReadOnlyEmpty()
	{
	}

	/// <summary>
	/// Determines whether the set contains the specified item.
	/// </summary>
	/// <param name="item">The item to locate. This value can be <see langword="null"/>.</param>
	/// <returns>Always <see langword="false"/>.</returns>
	public bool Contains(TValue item)
	{
		return false;
	}
	/// <summary>
	/// Determines whether the dictionary contains the specified key.
	/// </summary>
	/// <param name="key">The key to locate. This value must not be <see langword="null"/>.</param>
	/// <returns>Always <see langword="false"/>.</returns>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
	bool IReadOnlyDictionary<TKey, TValue>.ContainsKey(TKey key)
	{
		ArgumentNullException.ThrowIfNull(key);
		return false;
	}

	/// <summary>
	/// Returns an enumerator that yields no elements.
	/// </summary>
	/// <returns>An enumerator whose first call to <see cref="IEnumerator.MoveNext"/> returns <see langword="false"/>.</returns>
	public IEnumerator<TValue> GetEnumerator()
	{
		return Enumerable.Empty<TValue>().GetEnumerator();
	}

	/// <summary>
	/// Determines whether the empty set is a proper subset of the specified collection.
	/// </summary>
	/// <param name="other">The collection to compare with. This value must not be <see langword="null"/>.</param>
	/// <returns><see langword="true"/> when <paramref name="other"/> contains at least one element; otherwise, <see langword="false"/>.</returns>
	public bool IsProperSubsetOf(IEnumerable<TValue> other)
	{
		return other.AsValueEnumerable().Any();
	}

	/// <summary>
	/// Determines whether the empty set is a proper superset of the specified collection.
	/// </summary>
	/// <param name="other">The collection to compare with. It is not read.</param>
	/// <returns>Always <see langword="false"/>.</returns>
	public bool IsProperSupersetOf(IEnumerable<TValue> other)
	{
		return false;
	}

	/// <summary>
	/// Determines whether the empty set is a subset of the specified collection.
	/// </summary>
	/// <param name="other">The collection to compare with. It is not read.</param>
	/// <returns>Always <see langword="true"/>, because the empty set is a subset of every set.</returns>
	public bool IsSubsetOf(IEnumerable<TValue> other)
	{
		return true;
	}

	/// <summary>
	/// Determines whether the empty set is a superset of the specified collection.
	/// </summary>
	/// <param name="other">The collection to compare with. This value must not be <see langword="null"/>.</param>
	/// <returns><see langword="true"/> when <paramref name="other"/> is empty; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
	public bool IsSupersetOf(IEnumerable<TValue> other)
	{
		return !other.Any();
	}

	/// <summary>
	/// Determines whether the empty set shares any elements with the specified collection.
	/// </summary>
	/// <param name="other">The collection to compare with. It is not read.</param>
	/// <returns>Always <see langword="false"/>.</returns>
	public bool Overlaps(IEnumerable<TValue> other)
	{
		return false;
	}

	/// <summary>
	/// Determines whether the empty set and the specified collection contain the same elements.
	/// </summary>
	/// <param name="other">The collection to compare with. This value must not be <see langword="null"/>.</param>
	/// <returns><see langword="true"/> when <paramref name="other"/> is empty; otherwise, <see langword="false"/>.</returns>
	public bool SetEquals(IEnumerable<TValue> other)
	{
		return !other.AsValueEnumerable().Any();
	}

	/// <summary>
	/// Attempts to get the value associated with the specified key.
	/// </summary>
	/// <remarks>
	/// Unlike the indexer and <see cref="IReadOnlyDictionary{TKey, TValue}.ContainsKey(TKey)"/>, this method does not validate
	/// <paramref name="key"/>, so it does not throw when <paramref name="key"/> is <see langword="null"/>.
	/// </remarks>
	/// <param name="key">The key to look up. It is ignored.</param>
	/// <param name="value">When this method returns, contains the <see langword="default"/> value of <typeparamref name="TValue"/>.</param>
	/// <returns>Always <see langword="false"/>.</returns>
	public bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue value)
	{
		value = default!;
		return false;
	}

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	/// <summary>
	/// Returns an enumerator that yields no key/value pairs.
	/// </summary>
	/// <returns>An enumerator whose first call to <see cref="IEnumerator.MoveNext"/> returns <see langword="false"/>.</returns>
	IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
	{
		return Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();
	}
}
