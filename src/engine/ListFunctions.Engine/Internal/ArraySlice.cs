using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ListFunctions.Internal;

/// <summary>
/// Provides static factory methods for creating <see cref="ArraySlice{T}"/> instances.
/// </summary>
public static class ArraySlice
{
    /// <summary>
    /// Returns an empty slice of the specified array element type.
    /// </summary>
    /// <remarks>The returned slice has a length of zero. This instance
    /// can be reused wherever an empty slice is required.</remarks>
    /// <typeparam name="T">The type of elements in the array slice.</typeparam>
    /// <returns>An <see cref="ArraySlice{T}"/> instance over a span of zero elements.</returns>
    public static ArraySlice<T> Empty<T>() => EmptyInstance<T>.Default;

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

    private static class EmptyInstance<T>
    {
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
[StructLayout(LayoutKind.Sequential)]
[DebuggerStepThrough, DebuggerDisplay("Length = {Length}")]
public readonly struct ArraySlice<T> : IEnumerable<T>
{
    private readonly T[]? _array;
    private readonly int _length;
    private readonly int _offset;

    public T[] Array => _array ?? [];
    public readonly int Length => _length;
    public readonly int Offset => _offset;

    internal ArraySlice(T[] empty)
    {
        Debug.Assert(empty.Length == 0);
        _array = empty;
        _length = 0;
        _offset = 0;
    }
    public ArraySlice(T[] array, int length) : this(array, 0, length)
    {
    }
    public ArraySlice(T[] array, int offset, int length)
    {
        Guard.NotNull(array);
        Guard.ThrowIfNegativeOrGreaterThan(length, (uint)array.Length - (uint)offset, nameof(length));
        _array = array;
        _offset = offset;
        _length = length;
    }

    [DebuggerStepThrough, EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out T[] array, out int length, out int offset)
    {
        array = this.Array;
        length = _length;
        offset = _offset;
    }

    public ReadOnlySpan<T> AsSpan()
    {
        return _array is T[] array && array.Length > 0
            ? array.AsSpan(_offset, _length)
            : [];
    }

    public Enumerator GetEnumerator()
    {
        return new(this);
    }
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return new Enumerator(this);
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
        return new Enumerator(this);
    }

    public static implicit operator ReadOnlySpan<T>(ArraySlice<T> slice)
    {
        return slice._array is T[] array && array.Length > 0
            ? array.AsSpan(slice._offset, slice._length)
            : [];
    }
    public static implicit operator Span<T>(ArraySlice<T> slice)
    {
        return slice._array is T[] array && array.Length > 0
            ? array.AsSpan(slice._offset, slice._length)
            : [];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Enumerator : IEnumerator<T>
    {
        private T[] _array;
        private T _current;
        private int _index;
        private int _offset;
        private int _length;

        public readonly T Current => _current;
        readonly object? IEnumerator.Current => this.Current;

        internal Enumerator(ArraySlice<T> slice)
        {
            _array = slice._array ?? [];
            _offset = slice._offset;
            _length = slice._length;
            _index = -1;
            _current = default!;
        }

        public void Dispose()
        {
            this = default;
        }

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

        public void Reset()
        {
            _index = -1;
            _current = default!;
        }
    }
}