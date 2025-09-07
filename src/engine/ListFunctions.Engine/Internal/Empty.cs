using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using ZLinq;

namespace ListFunctions.Internal
{
    public static class Empty
    {
        public static IReadOnlySet<T> Set<T>() => EmptyHolder<object, T>.Default;
        public static IReadOnlyDictionary<TKey, TValue> Dictionary<TKey, TValue>() where TKey : notnull => EmptyHolder<TKey, TValue>.Default;

        private static class EmptyHolder<TKey, TValue> where TKey : notnull
        {
            public static readonly ReadOnlyEmpty<TKey, TValue> Default = new();
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    internal sealed class ReadOnlyEmpty<TKey, TValue> : IReadOnlyList<TValue>, IReadOnlySet<TValue>, IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key]
        {
            [DoesNotReturn]
            get
            {
                Guard.NotNull(key, nameof(key));
                throw new KeyNotFoundException();
            }
        }
        TValue IReadOnlyList<TValue>.this[int index] => default!;

        public int Count => 0;
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Array.Empty<TKey>();
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Array.Empty<TValue>();

        internal ReadOnlyEmpty()
        {
        }

        public bool Contains(TValue item)
        {
            return false;
        }
        bool IReadOnlyDictionary<TKey, TValue>.ContainsKey(TKey key)
        {
            Guard.NotNull(key, nameof(key));
            return false;
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            return Enumerable.Empty<TValue>().GetEnumerator();
        }

        public bool IsProperSubsetOf(IEnumerable<TValue> other)
        {
            return other.AsValueEnumerable().Any();
        }

        public bool IsProperSupersetOf(IEnumerable<TValue> other)
        {
            return false;
        }

        public bool IsSubsetOf(IEnumerable<TValue> other)
        {
            return true;
        }

        public bool IsSupersetOf(IEnumerable<TValue> other)
        {
            return !other.Any();
        }

        public bool Overlaps(IEnumerable<TValue> other)
        {
            return false;
        }

        public bool SetEquals(IEnumerable<TValue> other)
        {
            return !other.AsValueEnumerable().Any();
        }

        public bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue value)
        {
            value = default!;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();
        }
    }
}
