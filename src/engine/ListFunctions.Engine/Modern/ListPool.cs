using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ListFunctions.Modern
{
    public static class ListPool
    {
        private const int DEFAULT_LIST_CAPACITY = 50;
        private static List<object?>[] CreateLists(int count)
        {
            List<object?>[] array = new List<object?>[count];
            for (int i = 0; i < count; i++)
            {
                array[i] = new List<object?>(DEFAULT_LIST_CAPACITY);
            }

            return array;
        }
        private static readonly Lazy<ConcurrentBag<List<object?>>> _bag = new(
            () => new ConcurrentBag<List<object?>>(CreateLists(count: 2)));

        public static List<object?> Rent()
        {
            if (!_bag.Value.TryTake(out var list))
            {
                list = new List<object?>(DEFAULT_LIST_CAPACITY);
            }

            return list;
        }

        public static void Return(List<object?> list)
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
