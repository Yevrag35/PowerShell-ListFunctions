using System.ComponentModel;

namespace ListFunctions.Internal;

/// <summary>
/// Provides static factory methods for creating <see cref="ArraySlice{T}"/> instances.
/// </summary>
internal static class ArraySlice
{
	/// <summary>
	/// Creates a new <see cref="ArraySlice{T}"/> containing the specified values.
	/// </summary>
	/// <remarks>The returned slice is backed by a new array containing the specified values. Use <see
	/// cref="Empty{T}()"/> to obtain a shared empty slice instance.</remarks>
	/// <typeparam name="T">The type of elements to include in the slice.</typeparam>
	/// <param name="values">A sequence of values to populate the slice. May be empty.</param>
	/// <returns>An <see cref="ArraySlice{T}"/> containing the provided values. Returns an empty slice if <paramref name="values"/>
	/// is empty.</returns>
	public static ArraySlice<T> Create<T>(params ReadOnlySpan<T> values)
	{
		if (values.IsEmpty)
			return Empty<T>();

		return new(values.ToArray(), 0, values.Length);
	}
	/// <summary>
	/// Returns an empty slice of the specified array element type.
	/// </summary>
	/// <remarks>The returned slice has a length of zero. This instance
	/// can be reused wherever an empty slice is required.</remarks>
	/// <typeparam name="T">The type of elements in the array slice.</typeparam>
	/// <returns>An <see cref="ArraySlice{T}"/> instance over a span of zero elements.</returns>
	public static ArraySlice<T> Empty<T>() => EmptyInstance<T>.Default;

	/// <summary>
	/// Determines whether the specified <see cref="ArraySlice{T}"/> contains the given item.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array slice.</typeparam>
	/// <param name="slice">The array slice to search.</param>
	/// <param name="item">The item to locate in the array slice.</param>
	/// <returns><see langword="true"/> if the item is found in the array slice; otherwise, <see langword="false"/>.</returns>
	public static bool Contains<T>(ArraySlice<T> slice, T item) where T : notnull, IEquatable<T>
	{
		if (slice.Length == 0)
			return false;

		foreach (T element in slice.AsSpan())
		{
			if (element.Equals(item))
			{
				return true;
			}
		}

		return false;
	}
	/// <summary>
	/// Determines whether the specified <see cref="ArraySlice{T}"/> contains the given item, using the provided equality comparer.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array slice.</typeparam>
	/// <param name="slice">The array slice to search.</param>
	/// <param name="item">The item to locate in the array slice.</param>
	/// <param name="comparer">The equality comparer to use for comparing elements.</param>
	/// <returns><see langword="true"/> if the item is found in the array slice; otherwise, <see langword="false"/>.</returns>
	public static bool Contains<T>(ArraySlice<T> slice, T item, IEqualityComparer<T> comparer)
	{
		if (slice.Length == 0)
			return false;

		foreach (T element in slice.AsSpan())
		{
			if (comparer.Equals(element, item))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Holds the shared empty slice for one element type.
	/// </summary>
	/// <remarks>
	/// The runtime initializes the field once per closed generic type, so the instance is created lazily and without locking.
	/// </remarks>
	/// <typeparam name="T">The element type of the slice.</typeparam>
	private static class EmptyInstance<T>
	{
		/// <summary>
		/// The shared empty slice, backed by a zero-length array.
		/// </summary>
		internal static readonly ArraySlice<T> Default = new([]);
	}
}

/// <summary>
/// Represents a contiguous slice of an array, defined by an offset and length, without copying the underlying data.
/// </summary>
/// <remarks><see cref="ArraySlice{T}"/> provides a lightweight view over a segment of an array, allowing efficient access and
/// manipulation of a subset of its elements. The slice does not own the array; changes to the underlying array are
/// reflected in the slice. This type is useful for scenarios where working with subarrays is required without incurring
/// the cost of allocation or copying. <see cref="ArraySlice{T}"/> is a value type and is intended for performance-critical code. It
/// is not thread-safe if the underlying array is modified concurrently.</remarks>
/// <typeparam name="T">The type of elements contained in the array slice.</typeparam>
[DebuggerDisplay("Length = {Length}")]
[CollectionBuilder(typeof(ArraySlice), nameof(ArraySlice.Create))]
internal readonly struct ArraySlice<T> : IReadOnlyCollection<T>
{
	private readonly T[]? _array;
	private readonly int _length;
	private readonly int _offset;

	/// <summary>
	/// Gets or sets the element at the specified index within the slice.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The indexer does not check <paramref name="index"/> against <see cref="Length"/>. An index past the end of the slice
	/// reads or writes the elements of the underlying array that follow the slice, and the runtime throws only when the
	/// resulting position falls outside the array.
	/// </para>
	/// <para>
	/// Setting an element writes to the underlying array, so the change is visible to every slice and span over that array.
	/// </para>
	/// </remarks>
	/// <param name="index">The zero-based index of the element, relative to the start of the slice.</param>
	/// <value>The element located at the specified <paramref name="index"/>.</value>
	/// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> plus <see cref="Offset"/> is outside the bounds of the underlying array.</exception>
	/// <exception cref="NullReferenceException">Thrown when the slice is default-initialized.</exception>
	public T this[int index]
	{
		get => _array![_offset + index];
		set => _array![_offset + index] = value;
	}

	/// <summary>
	/// Gets the underlying array represented by this slice.
	/// </summary>
	/// <remarks>
	/// The array is returned as is, not copied, and it can be longer than the slice. Use <see cref="Offset"/> and
	/// <see cref="Length"/> to find the slice's elements in it.
	/// </remarks>
	/// <value>
	/// A reference to the whole underlying array, or <see langword="null"/> if the slice is default-initialized.
	/// </value>
	public T[]? Array => _array;
	/// <summary>
	/// Gets a value indicating whether the current <see cref="ArraySlice{T}"/> instance is default-initialized, meaning it has not been initialized with an underlying array.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the slice has no underlying array; otherwise, <see langword="false"/>. An empty slice from
	/// <see cref="ArraySlice.Empty{T}"/> is not default-initialized.
	/// </value>
	[MemberNotNullWhen(false, nameof(_array), nameof(Array))]
	public bool IsDefault => _array is null;
	/// <summary>
	/// Gets the number of elements in the slice.
	/// </summary>
	/// <value>The number of elements in the slice, or <c>0</c> if the slice is empty or default-initialized.</value>
	public readonly int Length => _length;
	/// <summary>
	/// Gets the zero-based offset in the underlying array where the slice begins.
	/// </summary>
	/// <value>The index in <see cref="Array"/> of the slice's first element, or <c>0</c> if the slice is default-initialized.</value>
	public readonly int Offset => _offset;

	/// <inheritdoc/>
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	int IReadOnlyCollection<T>.Count => _length;

	/// <summary>
	/// Initializes a new <see cref="ArraySlice{T}"/> instance that represents an empty slice over the specified zero-length array.
	/// </summary>
	/// <remarks>This constructor creates the shared empty slice. The provided array must have a length of zero, which is
	/// checked only in Debug builds.</remarks>
	/// <param name="empty">An array that must be empty. Used as the underlying storage for the empty slice.</param>
	internal ArraySlice(T[] empty)
	{
		Debug.Assert(empty.Length == 0);
		_array = empty;
		_length = 0;
		_offset = 0;
	}
	/// <summary>
	/// Initializes a new instance of the <see cref="ArraySlice{T}"/> struct that represents a slice of the specified array,
	/// starting at the beginning and containing the specified number of elements.
	/// </summary>
	/// <remarks>The slice references <paramref name="array"/> directly; the elements are not copied.</remarks>
	/// <param name="array">The array to create the slice from. This value must not be <see langword="null"/>.</param>
	/// <param name="length">The number of elements to include in the slice. Must be non-negative and not greater than the length of the array.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="array"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative or greater than the length of <paramref name="array"/>.</exception>
	public ArraySlice(T[] array, int length) : this(array, 0, length)
	{
	}
	/// <summary>
	/// Initializes a new <see cref="ArraySlice{T}"/> instance that represents the segment of the specified array that starts at <paramref name="offset"/> and contains <paramref name="length"/> elements.
	/// </summary>
	/// <remarks>
	/// <para>The slice references <paramref name="array"/> directly; the elements are not copied.</para>
	/// <para>TODO: <paramref name="offset"/> is not validated on its own. A negative offset, or one greater than the length
	/// of <paramref name="array"/>, makes the bounds check on <paramref name="length"/> unreliable.</para>
	/// </remarks>
	/// <param name="array">The array to create a slice from. This value must not be <see langword="null"/>.</param>
	/// <param name="offset">The zero-based index in the array at which the slice begins. Must be greater than or equal to 0 and less than or
	/// equal to the length of the array.</param>
	/// <param name="length">The number of elements in the slice. Must be non-negative and not exceed the number of elements from offset to the
	/// end of the array.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="array"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="length"/> is negative or greater than the number of elements from <paramref name="offset"/> to
	/// the end of <paramref name="array"/>.
	/// </exception>
	public ArraySlice(T[] array, int offset, int length)
	{
		ArgumentNullException.ThrowIfNull(array);
		Guard.ThrowIfNegativeOrGreaterThan(length, (uint)array.Length - (uint)offset, nameof(length));
		_array = array;
		_offset = offset;
		_length = length;
	}

	/// <summary>
	/// Deconstructs the slice into its underlying array, length, and offset components.
	/// </summary>
	/// <param name="array">When this method returns, contains the underlying array, or an empty array if the slice is default-initialized.</param>
	/// <param name="length">When this method returns, contains the number of elements in the slice.</param>
	/// <param name="offset">When this method returns, contains the zero-based offset at which the slice begins.</param>
	[DebuggerStepThrough, EditorBrowsable(EditorBrowsableState.Never)]
	public void Deconstruct(out T[] array, out int length, out int offset)
	{
		array = this.Array ?? [];
		length = _length;
		offset = _offset;
	}

	/// <summary>
	/// Returns a read-only span over the valid segment of the underlying array.
	/// </summary>
	/// <remarks>The returned span reflects the current state of the underlying array segment. Modifications to the
	/// array after obtaining the span are visible through the span. The span does not allocate memory.</remarks>
	/// <returns>A <see cref="ReadOnlySpan{T}"/> representing the elements in the current segment. Returns an empty span if the
	/// segment is empty.</returns>
	public ReadOnlySpan<T> AsSpan()
	{
		return _length > 0
			? _array.AsSpan(_offset, _length)
			: [];
	}

	/// <summary>
	/// Converts an <see cref="ArraySlice{T}"/> instance to a <see cref="ReadOnlySpan{T}"/> representing the sliced portion
	/// of the array.
	/// </summary>
	/// <remarks>If the underlying array is <see langword="null"/> or empty, the resulting <see cref="ReadOnlySpan{T}"/> is
	/// empty. This operator enables seamless use of <see cref="ArraySlice{T}"/> in APIs that accept <see
	/// cref="ReadOnlySpan{T}"/>. The conversion does not allocate.</remarks>
	/// <param name="slice">The <see cref="ArraySlice{T}"/> to convert to a <see cref="ReadOnlySpan{T}"/>.</param>
	/// <returns>A <see cref="ReadOnlySpan{T}"/> over the elements of <paramref name="slice"/>.</returns>
	public static implicit operator ReadOnlySpan<T>(ArraySlice<T> slice)
	{
		return slice._array is T[] array && array.Length > 0
			? new ReadOnlySpan<T>(array, slice._offset, slice._length)
			: [];
	}
	/// <summary>
	/// Converts an <see cref="ArraySlice{T}"/> instance to a <see cref="Span{T}"/> representing the sliced portion of the
	/// array.
	/// </summary>
	/// <remarks>If the underlying array is <see langword="null"/> or empty, the resulting <see cref="Span{T}"/> is empty. This
	/// operator enables seamless use of <see cref="ArraySlice{T}"/> in APIs that accept <see cref="Span{T}"/>. The conversion
	/// does not allocate, and writes through the span change the underlying array.</remarks>
	/// <param name="slice">The <see cref="ArraySlice{T}"/> to convert to a <see cref="Span{T}"/>.</param>
	/// <returns>A writable <see cref="Span{T}"/> over the elements of <paramref name="slice"/>.</returns>
	public static implicit operator Span<T>(ArraySlice<T> slice)
	{
		return slice._array is T[] array && array.Length > 0
			? array.AsSpan(slice._offset, slice._length)
			: [];
	}

	/// <summary>
	/// Returns an enumerator that iterates through the <see cref="ArraySlice{T}"/>.
	/// </summary>
	/// <remarks>The enumerator is a struct, so a <see langword="foreach"/> loop over the slice does not allocate. A
	/// default-initialized slice yields no elements.</remarks>
	/// <returns>An enumerator that can be used to iterate through the contiguous slice.</returns>
	[DebuggerStepThrough]
	public Enumerator GetEnumerator()
	{
		return new Enumerator(this);
	}
	/// <inheritdoc/>
	[DebuggerStepThrough]
	IEnumerator<T> IEnumerable<T>.GetEnumerator()
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
	/// Provides an enumerator for iterating over a slice of an array of type <typeparamref name="T"/>.
	/// </summary>
	/// <remarks>The enumerator reads the underlying array as it advances, so changes made to the array during enumeration
	/// are visible. It does not detect such changes.</remarks>
	[StructLayout(LayoutKind.Auto)]
	public struct Enumerator : IEnumerator<T>
	{
		private readonly T[] _array;
		private T _current;
		private int _index;
		private readonly int _length;
		private readonly int _offset;

		/// <summary>
		/// Initializes a new <see cref="Enumerator"/> instance for the specified array slice.
		/// </summary>
		/// <param name="slice">The array slice to enumerate. Must contain a valid array and range.</param>
		internal Enumerator(ArraySlice<T> slice)
		{
			_array = slice.Array!;
			_current = default!;
			_index = -1;
			_length = slice._length;
			_offset = slice._offset;
		}

		/// <summary>
		/// Gets the current value of type <typeparamref name="T"/>.
		/// </summary>
		/// <value>
		/// The element at the current position, or the <see langword="default"/> value of <typeparamref name="T"/> before the first
		/// call to <see cref="MoveNext"/> and after enumeration ends.
		/// </value>
		public readonly T Current => _current;

		/// <inheritdoc/>
		public bool MoveNext()
		{
			int next = _index + 1;
			if ((uint)next < (uint)_length)
			{
				_current = _array[_offset + next];
				_index = next;
				return true;
			}

			_index = _length;
			_current = default!;
			return false;
		}
		/// <summary>
		/// Does nothing. The enumerator can't be reset.
		/// </summary>
		/// <remarks>The method is <see langword="readonly"/> and leaves the position unchanged. To enumerate the slice again,
		/// call <see cref="GetEnumerator"/> for a new enumerator.</remarks>
		public readonly void Reset()
		{
		}
		/// <inheritdoc/>
		public readonly void Dispose()
		{
		}

		/// <inheritdoc/>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		readonly object? IEnumerator.Current => this.Current;
	}
}
