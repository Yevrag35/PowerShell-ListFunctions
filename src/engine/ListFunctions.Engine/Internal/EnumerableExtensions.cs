using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace ListFunctions.Internal
{
    internal static class EnumerableExtensions
    {
        const string COUNT = "Count";

        internal static bool TryGetCount(this IEnumerable nonGenCollection, out int count)
        {
            if (nonGenCollection is ICollection icol)
            {
                count = icol.Count;
                return true;
            }
            else if (nonGenCollection is Array arr)
            {
                count = arr.Length;
                return true;
            }

            Type type = nonGenCollection.GetType();
            PropertyInfo? countProp;
            try
            {
                countProp = type.GetProperty(COUNT, BindingFlags.Public | BindingFlags.Instance, null, typeof(int), Array.Empty<Type>(), Array.Empty<ParameterModifier>());

                if (countProp is null)
                {
                    count = 0;
                    return false;
                }

                if (countProp.GetValue(nonGenCollection) is int number)
                {
                    count = number;
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.Fail(e.Message);
            }

            count = 0;
            return false;
        }

#if NET5_0_OR_GREATER
        internal static bool TryGetCount<T>(this IEnumerable<T> enumerable, out int count)
        {
            return enumerable.TryGetNonEnumeratedCount(out count);
        }
#else
        internal static bool TryGetCount<T>(this IEnumerable<T> collection, out int count)
        {
            if (collection is ICollection<T> icol)
            {
                count = icol.Count;
                return true;
            }
            else
            {
                return TryGetCount(nonGenCollection: collection, out count);
            }
        }
#endif
    }
}
