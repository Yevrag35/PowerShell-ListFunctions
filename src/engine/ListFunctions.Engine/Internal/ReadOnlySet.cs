#if !NETSTANDARD2_0
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Collections.Generic.IReadOnlySet<>))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Collections.ObjectModel.ReadOnlySet<>))]

#else

using System;
using System.Collections;
using System.Collections.Generic;

namespace System.Collections.Generic
{
    public interface IReadOnlySet<T> : IReadOnlyCollection<T>
    {
        //
        // Summary:
        //     Determines if the set contains a specific item.
        //
        // Parameters:
        //   item:
        //     The item to check if the set contains.
        //
        // Returns:
        //     true if found; otherwise false.
        bool Contains(T item);
        //
        // Summary:
        //     Determines whether the current set is a proper (strict) subset of a specified
        //     collection.
        //
        // Parameters:
        //   other:
        //     The collection to compare to the current set.
        //
        // Returns:
        //     true if the current set is a proper subset of other; otherwise false.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     other is null.
        bool IsProperSubsetOf(IEnumerable<T> other);
        //
        // Summary:
        //     Determines whether the current set is a proper (strict) superset of a specified
        //     collection.
        //
        // Parameters:
        //   other:
        //     The collection to compare to the current set.
        //
        // Returns:
        //     true if the collection is a proper superset of other; otherwise false.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     other is null.
        bool IsProperSupersetOf(IEnumerable<T> other);
        //
        // Summary:
        //     Determine whether the current set is a subset of a specified collection.
        //
        // Parameters:
        //   other:
        //     The collection to compare to the current set.
        //
        // Returns:
        //     true if the current set is a subset of other; otherwise false.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     other is null.
        bool IsSubsetOf(IEnumerable<T> other);
        //
        // Summary:
        //     Determine whether the current set is a super set of a specified collection.
        //
        // Parameters:
        //   other:
        //     The collection to compare to the current set.
        //
        // Returns:
        //     true if the current set is a subset of other; otherwise false.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     other is null.
        bool IsSupersetOf(IEnumerable<T> other);
        //
        // Summary:
        //     Determines whether the current set overlaps with the specified collection.
        //
        // Parameters:
        //   other:
        //     The collection to compare to the current set.
        //
        // Returns:
        //     true if the current set and other share at least one common element; otherwise,
        //     false.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     other is null.
        bool Overlaps(IEnumerable<T> other);
        //
        // Summary:
        //     Determines whether the current set and the specified collection contain the same
        //     elements.
        //
        // Parameters:
        //   other:
        //     The collection to compare to the current set.
        //
        // Returns:
        //     true if the current set is equal to other; otherwise, false.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     other is null.
        bool SetEquals(IEnumerable<T> other);
    }
}
namespace System.Collections.ObjectModel
{
    public sealed class ReadOnlySet<T> : IReadOnlySet<T>, ISet<T>
    {
        readonly HashSet<T> _set;
        public ReadOnlySet(ISet<T> set)
        {
            _set = new HashSet<T>(set);
        }
        public int Count => _set.Count;

        public bool IsReadOnly => true;

        void ICollection<T>.Add(T item)
        {
            throw new NotSupportedException();
        }
        bool ISet<T>.Add(T item)
        {
            throw new NotImplementedException();
        }

        void ICollection<T>.Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(T item) => _set.Contains(item);

        public void CopyTo(T[] array, int arrayIndex)
        {
            _set.CopyTo(array, arrayIndex);
        }

        void ISet<T>.ExceptWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        public IEnumerator<T> GetEnumerator() => _set.GetEnumerator();

        void ISet<T>.IntersectWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            return _set.IsProperSubsetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            return _set.IsProperSupersetOf(other);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            return _set.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return _set.IsSupersetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            return _set.Overlaps(other);
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException();
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            return _set.SetEquals(other);
        }

        void ISet<T>.SymmetricExceptWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        void ISet<T>.UnionWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}

#endif