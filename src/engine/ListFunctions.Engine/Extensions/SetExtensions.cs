using System;
using System.Collections.Generic;
using System.Text;
using ZLinq;
#if NET5_0_OR_GREATER
using ZLinq.Linq;

namespace ListFunctions.Extensions
{
    public static class SetExtensions
    {
        public static void UnionWithRef<TEnumerator, T>(this HashSet<T> set, ref ValueEnumerable<TEnumerator, T> collection)
            where TEnumerator : struct, IValueEnumerator<T>
#if NET8_0_OR_GREATER
                                , allows ref struct
#endif
        {
            foreach (T item in collection)
            {
                _ = set.Add(item);
            }
        }
    }
}
#endif