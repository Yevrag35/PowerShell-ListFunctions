#if NETSTANDARD2_0

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Collections.Generic
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
	/// <summary>
	/// Provides a read-only abstraction of a set.
	/// </summary>
	/// <remarks>
	/// This interface polyfills the <c>IReadOnlySet&lt;T&gt;</c> interface from .NET 5 for the .NET Standard 2.0 build. Other
	/// targets use the runtime's own interface. Like the module's other polyfills, it is internal, so PowerShell can't
	/// resolve its name and it can't compete with a public copy in another module.
	/// </remarks>
	/// <typeparam name="T">The type of elements in the set.</typeparam>
	internal interface IReadOnlySet<T> : IReadOnlyCollection<T>
	{
		/// <summary>
		/// Determines whether the set contains a specific item.
		/// </summary>
		/// <param name="item">The item to locate in the set.</param>
		/// <returns><see langword="true"/> if the set contains <paramref name="item"/>; otherwise, <see langword="false"/>.</returns>
		bool Contains(T item);
		/// <summary>
		/// Determines whether the current set is a proper (strict) subset of a specified collection.
		/// </summary>
		/// <param name="other">The collection to compare to the current set.</param>
		/// <returns>
		/// <see langword="true"/> if the current set is a proper subset of <paramref name="other"/>; otherwise, <see langword="false"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
		bool IsProperSubsetOf(IEnumerable<T> other);
		/// <summary>
		/// Determines whether the current set is a proper (strict) superset of a specified collection.
		/// </summary>
		/// <param name="other">The collection to compare to the current set.</param>
		/// <returns>
		/// <see langword="true"/> if the current set is a proper superset of <paramref name="other"/>; otherwise, <see langword="false"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
		bool IsProperSupersetOf(IEnumerable<T> other);
		/// <summary>
		/// Determines whether the current set is a subset of a specified collection.
		/// </summary>
		/// <param name="other">The collection to compare to the current set.</param>
		/// <returns>
		/// <see langword="true"/> if the current set is a subset of <paramref name="other"/>; otherwise, <see langword="false"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
		bool IsSubsetOf(IEnumerable<T> other);
		/// <summary>
		/// Determines whether the current set is a superset of a specified collection.
		/// </summary>
		/// <param name="other">The collection to compare to the current set.</param>
		/// <returns>
		/// <see langword="true"/> if the current set is a superset of <paramref name="other"/>; otherwise, <see langword="false"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
		bool IsSupersetOf(IEnumerable<T> other);
		/// <summary>
		/// Determines whether the current set overlaps with the specified collection.
		/// </summary>
		/// <param name="other">The collection to compare to the current set.</param>
		/// <returns>
		/// <see langword="true"/> if the current set and <paramref name="other"/> share at least one common element; otherwise,
		/// <see langword="false"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
		bool Overlaps(IEnumerable<T> other);
		/// <summary>
		/// Determines whether the current set and the specified collection contain the same elements.
		/// </summary>
		/// <param name="other">The collection to compare to the current set.</param>
		/// <returns>
		/// <see langword="true"/> if the current set is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
		bool SetEquals(IEnumerable<T> other);
	}
}
namespace System.Collections.ObjectModel
{
	/// <summary>
	/// Represents a read-only set whose contents can't be changed after it is created.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This class polyfills the <c>ReadOnlySet&lt;T&gt;</c> class from .NET 9 for the .NET Standard 2.0 build. Other targets
	/// use the runtime's own class. Like the module's other polyfills, it is internal, so PowerShell can't resolve its name
	/// and it can't compete with a public copy in another module.
	/// </para>
	/// <para>
	/// Unlike the .NET 9 class, which wraps the set it is given, this polyfill copies the set into a new
	/// <see cref="HashSet{T}"/> that uses <see cref="EqualityComparer{T}.Default"/>. Later changes to the source set are not
	/// reflected, and a custom comparer on the source set, such as a case-insensitive one, is not carried over.
	/// </para>
	/// <para>
	/// The members of <see cref="ICollection{T}"/> and <see cref="ISet{T}"/> that would change the set throw a
	/// <see cref="NotSupportedException"/>, except <see cref="ISet{T}.Add(T)"/>, which throws a
	/// <see cref="NotImplementedException"/>.
	/// </para>
	/// <para>
	/// The set is safe for concurrent reads, because nothing can change it after construction.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">The type of elements in the set.</typeparam>
	internal sealed class ReadOnlySet<T> : IReadOnlySet<T>, ISet<T>
	{
		private readonly HashSet<T> _set;
		/// <summary>
		/// Initializes a new <see cref="ReadOnlySet{T}"/> instance that contains a copy of the elements of the specified set.
		/// </summary>
		/// <param name="set">The set whose elements to copy. This value must not be <see langword="null"/>.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="set"/> is null.</exception>
		public ReadOnlySet(ISet<T> set)
		{
			_set = new HashSet<T>(set);
		}
		/// <summary>
		/// Gets the number of elements in the set.
		/// </summary>
		/// <value>The number of elements in the set.</value>
		public int Count => _set.Count;

		/// <summary>
		/// Gets a value indicating whether the set is read-only.
		/// </summary>
		/// <value>Always <see langword="true"/>.</value>
		public bool IsReadOnly => true;

		/// <summary>
		/// Throws, because the set is read-only.
		/// </summary>
		/// <param name="item">The item to add. It is ignored.</param>
		/// <exception cref="NotSupportedException">Thrown on every call.</exception>
		void ICollection<T>.Add(T item)
		{
			throw new NotSupportedException();
		}
		/// <summary>
		/// Throws, because the set is read-only.
		/// </summary>
		/// <param name="item">The item to add. It is ignored.</param>
		/// <returns>This method never returns.</returns>
		/// <exception cref="NotImplementedException">Thrown on every call.</exception>
		bool ISet<T>.Add(T item)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Throws, because the set is read-only.
		/// </summary>
		/// <exception cref="NotSupportedException">Thrown on every call.</exception>
		void ICollection<T>.Clear()
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Determines whether the set contains a specific item.
		/// </summary>
		/// <param name="item">The item to locate in the set. This value can be <see langword="null"/>.</param>
		/// <returns><see langword="true"/> if the set contains <paramref name="item"/>; otherwise, <see langword="false"/>.</returns>
		public bool Contains(T item) => _set.Contains(item);

		/// <summary>
		/// Copies the elements of the set to an array, starting at the specified array index.
		/// </summary>
		/// <param name="array">The array to copy the elements to. This value must not be <see langword="null"/>.</param>
		/// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="array"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="arrayIndex"/> is negative.</exception>
		/// <exception cref="ArgumentException">
		/// Thrown when the number of elements in the set is greater than the space from <paramref name="arrayIndex"/> to the end
		/// of <paramref name="array"/>.
		/// </exception>
		public void CopyTo(T[] array, int arrayIndex)
		{
			_set.CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// Throws, because the set is read-only.
		/// </summary>
		/// <param name="other">The collection of items to remove. It is ignored.</param>
		/// <exception cref="NotSupportedException">Thrown on every call.</exception>
		void ISet<T>.ExceptWith(IEnumerable<T> other)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Returns an enumerator that iterates through the set.
		/// </summary>
		/// <remarks>
		/// The enumerator is boxed, because the method returns the interface type.
		/// </remarks>
		/// <returns>An enumerator for the set.</returns>
		public IEnumerator<T> GetEnumerator() => _set.GetEnumerator();

		/// <summary>
		/// Throws, because the set is read-only.
		/// </summary>
		/// <param name="other">The collection to intersect with. It is ignored.</param>
		/// <exception cref="NotSupportedException">Thrown on every call.</exception>
		void ISet<T>.IntersectWith(IEnumerable<T> other)
		{
			throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public bool IsProperSubsetOf(IEnumerable<T> other)
		{
			return _set.IsProperSubsetOf(other);
		}

		/// <inheritdoc/>
		public bool IsProperSupersetOf(IEnumerable<T> other)
		{
			return _set.IsProperSupersetOf(other);
		}

		/// <inheritdoc/>
		public bool IsSubsetOf(IEnumerable<T> other)
		{
			return _set.IsSubsetOf(other);
		}

		/// <inheritdoc/>
		public bool IsSupersetOf(IEnumerable<T> other)
		{
			return _set.IsSupersetOf(other);
		}

		/// <inheritdoc/>
		public bool Overlaps(IEnumerable<T> other)
		{
			return _set.Overlaps(other);
		}

		/// <summary>
		/// Throws, because the set is read-only.
		/// </summary>
		/// <param name="item">The item to remove. It is ignored.</param>
		/// <returns>This method never returns.</returns>
		/// <exception cref="NotSupportedException">Thrown on every call.</exception>
		bool ICollection<T>.Remove(T item)
		{
			throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public bool SetEquals(IEnumerable<T> other)
		{
			return _set.SetEquals(other);
		}

		/// <summary>
		/// Throws, because the set is read-only.
		/// </summary>
		/// <param name="other">The collection to compare with. It is ignored.</param>
		/// <exception cref="NotSupportedException">Thrown on every call.</exception>
		void ISet<T>.SymmetricExceptWith(IEnumerable<T> other)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Throws, because the set is read-only.
		/// </summary>
		/// <param name="other">The collection to add. It is ignored.</param>
		/// <exception cref="NotSupportedException">Thrown on every call.</exception>
		void ISet<T>.UnionWith(IEnumerable<T> other)
		{
			throw new NotSupportedException();
		}

		/// <inheritdoc/>
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
	}
}

#endif