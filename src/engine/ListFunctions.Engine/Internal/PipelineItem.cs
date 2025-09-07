using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ListFunctions.Internal
{
    /// <summary>
    /// Represents a single item in a pipeline, which can be treated as a collection with at most one element.
    /// </summary>
    /// <remarks>This struct provides a way to encapsulate a single object as a collection-like structure. It
    /// implements <see cref="IList"/> and <see cref="IReadOnlyList{T}"/> interfaces, allowing it to be used in
    /// scenarios where a collection is expected, but only one item (or none) is present.  The item can be accessed via
    /// the <see cref="Value"/> property or through the collection interfaces. If the item is null, the collection is
    /// considered empty.</remarks>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct PipelineItem : IList, IReadOnlyList<object?>
    {
        readonly object? _value;

        object? IReadOnlyList<object?>.this[int index]
        {
            get
            {
                if (index != 0)
                    throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0.");

                return _value;
            }
        }
        object? IList.this[int index]
        {
            get => index == 0 ? _value : throw new ArgumentOutOfRangeException(nameof(index));
            set => throw new NotSupportedException();
        }

        int IReadOnlyCollection<object?>.Count => _value is null ? 0 : 1;
        int ICollection.Count => _value is null ? 0 : 1;
        bool ICollection.IsSynchronized => false;
        bool IList.IsReadOnly => true;
        bool IList.IsFixedSize => true;
        object ICollection.SyncRoot => this;


#if NET6_0_OR_GREATER
        [MemberNotNullWhen(false, nameof(_value), nameof(Value))]
#endif
        public bool IsEmpty => _value is null;
        public object? Value => _value;

        public PipelineItem(object? item)
        {
            _value = item;
        }

        int IList.Add(object? value) => throw new NotSupportedException();
        public void AddToList(ref IList list)
        {
            list.Add(_value);
        }
        void IList.Clear() => throw new NotSupportedException();
        public bool Contains(object? value)
        {
            if (this.IsEmpty)
                return value is null;
            
            else if (value is null)
                return false;

            return _value!.Equals(value);
        }
        public void CopyTo(Array array, int index)
        {
            if (this.IsEmpty)
            {
                return;
            }

            array.SetValue(_value, index);
        }
        int IList.IndexOf(object? value)
        {
            return this.Contains(value) ? 0 : -1;
        }
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();
        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
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

        public struct Enumerator : IEnumerator<object?>
        {
            private short _index;

            public object? Current { get; }

            internal Enumerator(object? item)
            {
                this.Current = item;
                _index = -1;
            }

            readonly void IDisposable.Dispose()
            {
                return;
            }

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

            void IEnumerator.Reset()
            {
                _index = -1;
            }
        }
    }
}

