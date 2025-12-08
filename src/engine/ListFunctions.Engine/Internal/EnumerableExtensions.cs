using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ListFunctions.Internal
{
    internal static class EnumerableExtensions
    {
#if !NET5_0_OR_GREATER
        internal static bool TryGetNonEnumeratedCount<T>(this IEnumerable<T> collection, out int count)
        {
            switch (collection)
            {
                case IReadOnlyCollection<T> roCol:
                    count = roCol.Count;
                    return true;

                case ICollection<T> icol:
                    count = icol.Count;
                    return true;

                case ICollection nonGenCol:
                    count = nonGenCol.Count;
                    return true;

                default:
                    count = 0;
                    return false;
            }
        }
#endif
    }
}
