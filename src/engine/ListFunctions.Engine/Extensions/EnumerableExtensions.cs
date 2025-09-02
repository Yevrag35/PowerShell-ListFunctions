#if !NETCOREAPP
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace ZLinq
{
    public static class EnumerableExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> AsValueEnumerable<T>(this IEnumerable<T> source)
        {
            return source;
        }
    }
}

#endif