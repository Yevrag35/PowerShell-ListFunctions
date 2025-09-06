using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ListFunctions.Modern.Pools
{
    public static class ListPool<T> where T : class?
    {
        private const int DEFAULT_LIST_CAPACITY = 50;
        private static List<T>[] CreateLists(int count)
        {
            var array = new List<T>[count];
            for (int i = 0; i < count; i++)
            {
                array[i] = new List<T>(DEFAULT_LIST_CAPACITY);
            }

            return array;
        }
        private static readonly Lazy<ConcurrentBag<List<T>>> _bag = new(
            () => new ConcurrentBag<List<T>>(CreateLists(count: 2)));

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
