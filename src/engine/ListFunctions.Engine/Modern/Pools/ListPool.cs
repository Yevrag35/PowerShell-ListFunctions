using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ListFunctions.Modern.Pools
{
#if !NETCOREAPP
    public
#else
    internal 
#endif
        static class ListPool<T> where T : class?
    {
        private const int DEFAULT_LIST_CAPACITY = 10;
        private static readonly Lazy<ConcurrentBag<List<T>>> _bag = new(
            () => new ConcurrentBag<List<T>>() { new List<T>(DEFAULT_LIST_CAPACITY) });

        public static List<T> Rent()
        {
            if (!_bag.Value.TryTake(out var list))
            {
                list = new List<T>(DEFAULT_LIST_CAPACITY);
            }

            return list;
        }

        public static void Return(List<T> list)
        {
            Guard.NotNull(list, nameof(list));
            list.Clear();
#if NET5_0_OR_GREATER
            list.EnsureCapacity(DEFAULT_LIST_CAPACITY);
#endif

            _bag.Value.Add(list);
        }
    }
}
