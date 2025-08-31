using System.Collections.Generic;

namespace ListFunctions.Extensions
{
    public static class DictionaryExtensions
    {
#if !NET5_0_OR_GREATER
        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value) where TKey : notnull
        {
            Guard.NotNull(dictionary, nameof(dictionary));
            Guard.NotNull(key, nameof(key));

            bool added = false;
            if (!dictionary.ContainsKey(key))
            {
                dictionary.Add(key, value);
                added = true;
            }

            return added;
        }
#endif
    }
}
