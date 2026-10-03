namespace ListFunctions.Internal;

/// <summary>
/// Represents a single item in a pipeline, which can be treated as a collection with at most one element.
/// </summary>
/// <remarks>
/// <para>
/// This struct provides a way to encapsulate a single object as a collection-like structure. It implements the
/// <see cref="IList"/> and <see cref="IReadOnlyList{T}"/> interfaces, allowing it to be used in scenarios where a collection is
/// expected, but only one item (or none) is present. The item can be accessed through the <see cref="Value"/> property or
/// through the collection interfaces. If the item is <see langword="null"/>, the collection is considered empty.
/// </para>
/// <para>
/// The collection is read-only and fixed-size. The <see cref="IList"/> members that would change it throw a
/// <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// The enumerator returned by <see cref="GetEnumerator"/> always yields exactly one element, even when the instance is
/// empty, in which case the element is <see langword="null"/>. This differs from the count reported through
/// <see cref="ICollection.Count"/> and <see cref="IReadOnlyCollection{T}.Count"/>, which is <c>0</c> for an empty instance.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PipelineItem : IList, IReadOnlyList<object?>
{
	readonly object? _value;

	/// <summary>
	/// Gets the item at the specified index.
	/// </summary>
	/// <remarks>
	/// An empty instance returns <see langword="null"/> for index <c>0</c> instead of throwing.
	/// </remarks>
	/// <param name="index">The zero-based index of the item. Only <c>0</c> is valid.</param>
	/// <value>The wrapped item, which can be <see langword="null"/>.</value>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is not 0.</exception>
	object? IReadOnlyList<object?>.this[int index]
	{
		get
		{
			if (index != 0)
				throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0.");

			return _value;
		}
	}
	/// <summary>
	/// Gets the item at the specified index. Setting an item is not supported.
	/// </summary>
	/// <remarks>
	/// An empty instance returns <see langword="null"/> for index <c>0</c> instead of throwing.
	/// </remarks>
	/// <param name="index">The zero-based index of the item. Only <c>0</c> is valid.</param>
	/// <value>The wrapped item, which can be <see langword="null"/>.</value>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when getting an item and <paramref name="index"/> is not 0.</exception>
	/// <exception cref="NotSupportedException">Thrown when setting an item.</exception>
	object? IList.this[int index]
	{
		get => index == 0 ? _value : throw new ArgumentOutOfRangeException(nameof(index));
		set => throw new NotSupportedException();
	}

	/// <summary>
	/// Gets the number of items in the collection.
	/// </summary>
	/// <value><c>0</c> when the wrapped item is <see langword="null"/>; otherwise, <c>1</c>.</value>
	int IReadOnlyCollection<object?>.Count => _value is null ? 0 : 1;
	/// <summary>
	/// Gets the number of items in the collection.
	/// </summary>
	/// <value><c>0</c> when the wrapped item is <see langword="null"/>; otherwise, <c>1</c>.</value>
	int ICollection.Count => _value is null ? 0 : 1;
	/// <summary>
	/// Gets a value indicating whether access to the collection is synchronized.
	/// </summary>
	/// <value>Always <see langword="false"/>.</value>
	bool ICollection.IsSynchronized => false;
	/// <summary>
	/// Gets a value indicating whether the collection is read-only.
	/// </summary>
	/// <value>Always <see langword="true"/>.</value>
	bool IList.IsReadOnly => true;
	/// <summary>
	/// Gets a value indicating whether the collection has a fixed size.
	/// </summary>
	/// <value>Always <see langword="true"/>.</value>
	bool IList.IsFixedSize => true;
	/// <summary>
	/// Gets an object that can be used to synchronize access to the collection.
	/// </summary>
	/// <remarks>
	/// Each call boxes the struct into a new object, so two calls never return the same instance. Locking on the result
	/// does not synchronize anything.
	/// </remarks>
	/// <value>A boxed copy of this instance.</value>
	object ICollection.SyncRoot => this;

	/// <summary>
	/// Gets a value indicating whether the wrapped item is <see langword="null"/>.
	/// </summary>
	/// <value><see langword="true"/> when <see cref="Value"/> is <see langword="null"/>; otherwise, <see langword="false"/>.</value>
	[MemberNotNullWhen(false, nameof(_value), nameof(Value))]
	public bool IsEmpty => _value is null;
	/// <summary>
	/// Gets the wrapped item.
	/// </summary>
	/// <value>The item that was passed to the constructor, which can be <see langword="null"/>.</value>
	public object? Value => _value;

	/// <summary>
	/// Initializes a new <see cref="PipelineItem"/> instance that wraps the specified item.
	/// </summary>
	/// <param name="item">The item to wrap. This value can be <see langword="null"/>, which makes the instance empty.</param>
	public PipelineItem(object? item)
	{
		_value = item;
	}

	/// <summary>
	/// Throws, because the collection is read-only.
	/// </summary>
	/// <param name="value">The item to add. It is ignored.</param>
	/// <returns>This method never returns.</returns>
	/// <exception cref="NotSupportedException">Thrown on every call.</exception>
	int IList.Add(object? value) => throw new NotSupportedException();
	/// <summary>
	/// Adds the wrapped item to the specified list.
	/// </summary>
	/// <remarks>
	/// The item is added even when the instance is empty, in which case <see langword="null"/> is added. The method does
	/// not assign a new list to <paramref name="list"/>.
	/// </remarks>
	/// <param name="list">The list to add the item to. This value must not be <see langword="null"/>.</param>
	/// <exception cref="NullReferenceException">Thrown when <paramref name="list"/> is null.</exception>
	/// <exception cref="NotSupportedException">Thrown when <paramref name="list"/> is read-only or has a fixed size.</exception>
	public void AddToList(ref IList list)
	{
		list.Add(_value);
	}
	/// <summary>
	/// Throws, because the collection is read-only.
	/// </summary>
	/// <exception cref="NotSupportedException">Thrown on every call.</exception>
	void IList.Clear() => throw new NotSupportedException();
	/// <summary>
	/// Determines whether the wrapped item equals the specified value.
	/// </summary>
	/// <remarks>
	/// The comparison calls <see cref="object.Equals(object)"/> on the wrapped item. An empty instance reports that it
	/// contains <see langword="null"/>, even though its count is <c>0</c>.
	/// </remarks>
	/// <param name="value">The value to locate. This value can be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when the instance is empty and <paramref name="value"/> is <see langword="null"/>, or when the wrapped
	/// item equals <paramref name="value"/>; otherwise, <see langword="false"/>.
	/// </returns>
	public bool Contains(object? value)
	{
		if (this.IsEmpty)
			return value is null;

		else if (value is null)
			return false;

		return _value!.Equals(value);
	}
	/// <summary>
	/// Copies the wrapped item to the specified array at the specified index.
	/// </summary>
	/// <remarks>
	/// When the instance is empty, the method copies nothing and does not validate its arguments.
	/// </remarks>
	/// <param name="array">The one-dimensional array to copy the item to. This value must not be <see langword="null"/> when the instance is not empty.</param>
	/// <param name="index">The zero-based index in <paramref name="array"/> at which to store the item.</param>
	/// <exception cref="NullReferenceException">Thrown when the instance is not empty and <paramref name="array"/> is null.</exception>
	/// <exception cref="IndexOutOfRangeException">Thrown when the instance is not empty and <paramref name="index"/> is outside the bounds of <paramref name="array"/>.</exception>
	/// <exception cref="InvalidCastException">Thrown when the wrapped item can't be stored in an element of <paramref name="array"/>.</exception>
	public void CopyTo(Array array, int index)
	{
		if (this.IsEmpty)
		{
			return;
		}

		array.SetValue(_value, index);
	}
	/// <summary>
	/// Returns the index of the specified value in the collection.
	/// </summary>
	/// <param name="value">The value to locate. This value can be <see langword="null"/>.</param>
	/// <returns><c>0</c> when <see cref="Contains(object)"/> returns <see langword="true"/> for <paramref name="value"/>; otherwise, <c>-1</c>.</returns>
	int IList.IndexOf(object? value)
	{
		return this.Contains(value) ? 0 : -1;
	}
	/// <summary>
	/// Throws, because the collection is read-only.
	/// </summary>
	/// <param name="index">The index at which to insert. It is ignored.</param>
	/// <param name="value">The item to insert. It is ignored.</param>
	/// <exception cref="NotSupportedException">Thrown on every call.</exception>
	void IList.Insert(int index, object? value) => throw new NotSupportedException();
	/// <summary>
	/// Throws, because the collection is read-only.
	/// </summary>
	/// <param name="value">The item to remove. It is ignored.</param>
	/// <exception cref="NotSupportedException">Thrown on every call.</exception>
	void IList.Remove(object? value) => throw new NotSupportedException();
	/// <summary>
	/// Throws, because the collection is read-only.
	/// </summary>
	/// <param name="index">The index of the item to remove. It is ignored.</param>
	/// <exception cref="NotSupportedException">Thrown on every call.</exception>
	void IList.RemoveAt(int index) => throw new NotSupportedException();
	/// <summary>
	/// Returns an enumerator that iterates through the collection.
	/// </summary>
	/// <remarks>
	/// The enumerator yields the wrapped item exactly once, even when it is <see langword="null"/>. The enumerator is a struct,
	/// so a <see langword="foreach"/> loop over a <see cref="PipelineItem"/> does not allocate.
	/// </remarks>
	/// <returns>An enumerator that can be used to iterate through the collection.</returns>
	public Enumerator GetEnumerator()
	{
		return new Enumerator(_value);
	}
	/// <inheritdoc/>
	[DebuggerStepThrough]
	IEnumerator<object?> IEnumerable<object?>.GetEnumerator()
	{
		return this.GetEnumerator();
	}
	/// <inheritdoc/>
	[DebuggerStepThrough]
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	/// <summary>
	/// Enumerates the single item of a <see cref="PipelineItem"/>.
	/// </summary>
	/// <remarks>
	/// The enumerator yields one element and then stops. It does not check whether the item is <see langword="null"/>, so an
	/// empty <see cref="PipelineItem"/> still yields one <see langword="null"/> element.
	/// </remarks>
	[StructLayout(LayoutKind.Auto)]
	public struct Enumerator : IEnumerator<object?>
	{
		private short _index;

		/// <summary>
		/// Gets the item that the enumerator yields.
		/// </summary>
		/// <remarks>
		/// The property returns the item at any position, including before the first call to <see cref="MoveNext"/> and after
		/// enumeration ends.
		/// </remarks>
		/// <value>The wrapped item, which can be <see langword="null"/>.</value>
		public object? Current { get; }

		/// <summary>
		/// Initializes a new <see cref="Enumerator"/> instance that yields the specified item.
		/// </summary>
		/// <param name="item">The item to yield. This value can be <see langword="null"/>.</param>
		internal Enumerator(object? item)
		{
			this.Current = item;
			_index = -1;
		}

		/// <summary>
		/// Does nothing, because the enumerator holds no resources.
		/// </summary>
		readonly void IDisposable.Dispose()
		{
			return;
		}

		/// <summary>
		/// Advances the enumerator to the item.
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
		/// Sets the enumerator to its initial position, before the item.
		/// </summary>
		void IEnumerator.Reset()
		{
			_index = -1;
		}
	}
}

