using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

#nullable enable

namespace ListFunctions.Modern.Pools
{
    internal static class ObjPool<T> where T : class, IPoolable, new()
    {
        private static readonly Lazy<ConcurrentBag<T>> _bag = new(() =>
        {
            return new ConcurrentBag<T>() { new T() };
        });

        public static T Rent()
        {
            if (!_bag.Value.TryTake(out T? item))
            {
                item = new T();
            }

            item.Initialize();
            return item;
        }

        public static void Return(T returningItem)
        {
            Guard.NotNull(returningItem, nameof(returningItem));
            if (returningItem.TryReset())
            {
                _bag.Value.Add(returningItem);
            }
        }
    }
}

