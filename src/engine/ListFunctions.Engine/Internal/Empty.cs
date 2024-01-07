using ListFunctions.Internal;
using MG.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ListFunctions
{
    public static class Empty<T>
    {
        public static IReadOnlyList<T> List => Array.Empty<T>();
        public static IReadOnlySet<T> Set => ReadOnlyEmpty<object, T>.Default;
    }
    public static class Empty<TKey, TValue> where TKey : notnull
    {
        public static IReadOnlyDictionary<TKey, TValue> Dictionary => ReadOnlyEmpty<TKey, TValue>.Default;
    }

    internal readonly struct ReadOnlyEmpty<TKey, TValue> : IReadOnlyList<TValue>, IReadOnlySet<TValue>, IReadOnlyDictionary<TKey, TValue>
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
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Enumerable.Empty<TKey>();
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Enumerable.Empty<TValue>();

        internal static ReadOnlyEmpty<TKey, TValue> Default => default;

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
            return other.Any();
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
            return !other.Any();
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
