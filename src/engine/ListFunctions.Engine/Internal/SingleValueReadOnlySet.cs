using ZLinq;

namespace ListFunctions.Internal;

/// <summary>
/// Provides factory methods for creating <see cref="SingleValueReadOnlySet{T}"/> instances.
/// </summary>
internal static class SingleValueReadOnlySet
{
	/// <summary>
	/// Creates a read-only set that contains the first element of the specified collection.
	/// </summary>
	/// <remarks>
	/// Only the first element is read. Any other elements in <paramref name="collection"/> are ignored.
	/// </remarks>
	/// <typeparam name="T">The element type of the set.</typeparam>
	/// <param name="collection">The collection whose first element becomes the set's value. It must contain at least one element.</param>
	/// <param name="equalityComparer">
	/// The comparer to use for set operations, or <see langword="null"/> to use the default comparer for
	/// <typeparamref name="T"/>, which is <see cref="StringComparer.OrdinalIgnoreCase"/> for <see cref="string"/>.
	/// </param>
	/// <returns>A new set that contains the first element of <paramref name="collection"/>.</returns>
	/// <exception cref="InvalidOperationException">Thrown when <paramref name="collection"/> is empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the first element of <paramref name="collection"/> is null.</exception>
	internal static SingleValueReadOnlySet<T> Create<T>(IEnumerable<T> collection, IEqualityComparer<T>? equalityComparer = null) where T : notnull
	{
		return new SingleValueReadOnlySet<T>(collection.AsValueEnumerable().First(), equalityComparer);
	}
	/// <summary>
	/// Creates a read-only set that contains the specified value.
	/// </summary>
	/// <typeparam name="T">The element type of the set.</typeparam>
	/// <param name="value">The value of the set. This value must not be <see langword="null"/>.</param>
	/// <param name="equalityComparer">
	/// The comparer to use for set operations, or <see langword="null"/> to use the default comparer for
	/// <typeparamref name="T"/>, which is <see cref="StringComparer.OrdinalIgnoreCase"/> for <see cref="string"/>.
	/// </param>
	/// <returns>A new set that contains <paramref name="value"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
	internal static SingleValueReadOnlySet<T> Create<T>(T value, IEqualityComparer<T>? equalityComparer = null) where T : notnull
	{
		return new SingleValueReadOnlySet<T>(value, equalityComparer);
	}
}

/// <summary>
/// Represents an immutable set that contains exactly one value.
/// </summary>
/// <remarks>
/// <para>
/// The set operations compare elements with the comparer that was supplied when the set was created. Without one, the set
/// uses <see cref="StringComparer.OrdinalIgnoreCase"/> for <see cref="string"/>, to match PowerShell's case-insensitive
/// defaults, and <see cref="EqualityComparer{T}.Default"/> for any other type.
/// </para>
/// <para>
/// A <see langword="default"/> instance holds no comparer. When <typeparamref name="T"/> is a reference type, such an instance
/// is empty: <see cref="IsEmpty"/> is <see langword="true"/> and the set operations treat it as the empty set, although
/// <see cref="Count"/> still returns <c>1</c>. When <typeparamref name="T"/> is a value type, a <see langword="default"/>
/// instance is not empty, and the set operations that compare elements throw a <see cref="NullReferenceException"/>.
/// </para>
/// <para>
/// The set is immutable, so it is thread-safe when <typeparamref name="T"/> and the comparer are.
/// </para>
/// </remarks>
/// <typeparam name="T">The element type of the set. Elements can't be <see langword="null"/>.</typeparam>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct SingleValueReadOnlySet<T> : IReadOnlySet<T> where T : notnull
{
	readonly IEqualityComparer<T> _equality;
	readonly T _value;

	/// <summary>
	/// Gets the number of elements in the set.
	/// </summary>
	/// <value>Always <c>1</c>, including for a <see langword="default"/> instance.</value>
	public int Count => 1;
	/// <summary>
	/// Gets a value indicating whether the set holds no value.
	/// </summary>
	/// <remarks>
	/// Only a <see langword="default"/> instance whose <typeparamref name="T"/> is a reference type is empty, because the
	/// constructor rejects <see langword="null"/>.
	/// </remarks>
	/// <value><see langword="true"/> when the stored value is <see langword="null"/>; otherwise, <see langword="false"/>.</value>
	internal bool IsEmpty => _value is null;
	/// <summary>
	/// Gets the single value of the set.
	/// </summary>
	/// <value>The value that the set was created with.</value>
	internal T Value => _value;

	/// <summary>
	/// Initializes a new <see cref="SingleValueReadOnlySet{T}"/> instance with the specified value and equality comparer.
	/// </summary>
	/// <param name="value">The value of the set. This value must not be <see langword="null"/>.</param>
	/// <param name="equalityComparer">
	/// The comparer to use for set operations, or <see langword="null"/> to use the default comparer for
	/// <typeparamref name="T"/>, which is <see cref="StringComparer.OrdinalIgnoreCase"/> for <see cref="string"/>.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
	public SingleValueReadOnlySet(T value, IEqualityComparer<T>? equalityComparer)
	{
		if (value is null)
		{
			throw new ArgumentNullException(nameof(value));
		}

		_equality = ResolveComparer(equalityComparer);
		_value = value;
	}

	/// <summary>
	/// Returns the supplied comparer, or the default comparer for <typeparamref name="T"/> when none is supplied.
	/// </summary>
	/// <param name="supplied">The comparer to use. This value can be <see langword="null"/>.</param>
	/// <returns>
	/// <paramref name="supplied"/> when it is not <see langword="null"/>; otherwise, <see cref="StringComparer.OrdinalIgnoreCase"/>
	/// when <typeparamref name="T"/> is <see cref="string"/>, or <see cref="EqualityComparer{T}.Default"/> for any other type.
	/// </returns>
	private static IEqualityComparer<T> ResolveComparer(IEqualityComparer<T>? supplied)
	{
		if (supplied is null)
		{
			supplied = typeof(string).Equals(typeof(T))
				? (IEqualityComparer<T>)StringComparer.OrdinalIgnoreCase
				: EqualityComparer<T>.Default;
		}

		return supplied;
	}

	/// <summary>
	/// Determines whether the set contains the specified item.
	/// </summary>
	/// <param name="item">The item to locate.</param>
	/// <returns>
	/// <see langword="true"/> when the set is not empty and its value equals <paramref name="item"/>; otherwise,
	/// <see langword="false"/>.
	/// </returns>
	public bool Contains(T item)
	{
		return !this.IsEmpty && _equality.Equals(_value, item);
	}

	/// <summary>
	/// Determines whether the set is a proper subset of the specified collection.
	/// </summary>
	/// <remarks>
	/// The method stops enumerating <paramref name="other"/> as soon as it has seen both the set's value and an element that
	/// differs from it.
	/// </remarks>
	/// <param name="other">The collection to compare with. This value must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when <paramref name="other"/> contains the set's value and at least one other element, or when the set
	/// is empty and <paramref name="other"/> is not; otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
	public bool IsProperSubsetOf(IEnumerable<T> other)
	{
		Guard.NotNull(other, nameof(other));
		if (this.IsEmpty)
		{
			return other.AsValueEnumerable().Any();
		}

		var enumerator = other.AsValueEnumerable().GetEnumerator();
		DoubleBool dub = DoubleBool.InitializeNew();
		while (!dub && enumerator.MoveNext())
		{
			if (_equality.Equals(_value, enumerator.Current))
			{
				dub.Bool1 = true;
			}
			else
			{
				dub.Bool2 = true;
			}
		}

		return dub;
	}

	/// <summary>
	/// Determines whether the set is a proper superset of the specified collection.
	/// </summary>
	/// <param name="other">The collection to compare with. This value must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when the set is not empty and <paramref name="other"/> is empty; otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
	public bool IsProperSupersetOf(IEnumerable<T> other)
	{
		Guard.NotNull(other, nameof(other));
		if (this.IsEmpty)
		{
			return false;
		}

		return !other.AsValueEnumerable().Any();
	}

	/// <summary>
	/// Determines whether the set is a subset of the specified collection.
	/// </summary>
	/// <param name="other">The collection to compare with. This value must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when the set is empty or <paramref name="other"/> contains the set's value; otherwise,
	/// <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
	public bool IsSubsetOf(IEnumerable<T> other)
	{
		Guard.NotNull(other, nameof(other));
		if (this.IsEmpty)
		{
			return true;
		}

		foreach (T item in other.AsValueEnumerable())
		{
			if (_equality.Equals(_value, item))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether the set is a superset of the specified collection.
	/// </summary>
	/// <param name="other">The collection to compare with. This value must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when every element of <paramref name="other"/> equals the set's value, including when
	/// <paramref name="other"/> is empty; otherwise, <see langword="false"/>. An empty set is a superset only of an empty
	/// collection.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
	public bool IsSupersetOf(IEnumerable<T> other)
	{
		Guard.NotNull(other, nameof(other));
		if (this.IsEmpty)
		{
			return !other.AsValueEnumerable().Any();
		}

		foreach (T item in other.AsValueEnumerable())
		{
			if (!_equality.Equals(_value, item))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Determines whether the set and the specified collection share any element.
	/// </summary>
	/// <param name="other">The collection to compare with. This value must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when the set is not empty and <paramref name="other"/> contains its value; otherwise,
	/// <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
	public bool Overlaps(IEnumerable<T> other)
	{
		Guard.NotNull(other, nameof(other));
		if (this.IsEmpty)
		{
			return false;
		}

		foreach (T item in other.AsValueEnumerable())
		{
			if (_equality.Equals(_value, item))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether the set and the specified collection contain the same elements.
	/// </summary>
	/// <remarks>
	/// The method compares <paramref name="other"/> as a sequence, not as a set. A collection that holds the set's value more
	/// than once, such as <c>@('a', 'a')</c>, is not equal to the set. When the count of <paramref name="other"/> is available
	/// without enumeration and is not <c>1</c>, the method returns without enumerating it.
	/// </remarks>
	/// <param name="other">The collection to compare with. This value must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when <paramref name="other"/> contains exactly one element and it equals the set's value, or when
	/// both the set and <paramref name="other"/> are empty; otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
	public bool SetEquals(IEnumerable<T> other)
	{
		Guard.NotNull(other, nameof(other));
		if (this.IsEmpty)
		{
			return !other.AsValueEnumerable().Any();
		}

		if (other.TryGetNonEnumeratedCount(out int count) && count != 1)
		{
			return false;
		}

		return other.AsValueEnumerable().SequenceEqual(this, _equality);
	}

	/// <summary>
	/// Returns an enumerator that yields the set's value.
	/// </summary>
	/// <remarks>
	/// The enumerator is a struct, so a <see langword="foreach"/> loop over the set does not allocate.
	/// </remarks>
	/// <returns>An enumerator that yields the set's value once.</returns>
	public Enumerator GetEnumerator()
	{
		return new(_value);
	}
	/// <inheritdoc/>
	IEnumerator<T> IEnumerable<T>.GetEnumerator()
	{
		return this.GetEnumerator();
	}
	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	/// <summary>
	/// Enumerates the single value of a <see cref="SingleValueReadOnlySet{T}"/>.
	/// </summary>
	/// <remarks>
	/// The enumerator yields one element and then stops. It does not check whether the set is empty, so a
	/// <see langword="default"/> set still yields one <see langword="default"/> element.
	/// </remarks>
	public struct Enumerator : IEnumerator<T>
	{
		readonly T _value;
		int _index;
		/// <summary>
		/// Gets the value that the enumerator yields.
		/// </summary>
		/// <remarks>
		/// The property returns the value at any position, including before the first call to <see cref="MoveNext"/> and after
		/// enumeration ends.
		/// </remarks>
		/// <value>The set's value.</value>
		public readonly T Current => _value;
		/// <inheritdoc/>
		readonly object? IEnumerator.Current => this.Current;

		/// <summary>
		/// Initializes a new <see cref="Enumerator"/> instance that yields the specified value.
		/// </summary>
		/// <param name="value">The value to yield.</param>
		internal Enumerator(T value)
		{
			_value = value;
			_index = -1;
		}

		/// <summary>
		/// Advances the enumerator to the value.
		/// </summary>
		/// <returns><see langword="true"/> on the first call; otherwise, <see langword="false"/>.</returns>
		public bool MoveNext()
		{
			if (_index == -1)
			{
				_index = 0;
				return true;
			}

			_index = 1;
			return false;
		}
		/// <summary>
		/// Sets the enumerator to its initial position, before the value.
		/// </summary>
		public void Reset()
		{
			_index = -1;
		}
		/// <summary>
		/// Does nothing, because the enumerator holds no resources.
		/// </summary>
		public readonly void Dispose()
		{
		}
	}
}
