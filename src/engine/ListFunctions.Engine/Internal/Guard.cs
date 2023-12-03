using System;
using System.Diagnostics.CodeAnalysis;

namespace ListFunctions.Internal
{
    internal static class Guard
    {
        /// <exception cref="ArgumentNullException"/>
        internal static void NotNull<T>([NotNull] T? value, string? parameterName) where T : class
        {
            if (value is null)
            {
                parameterName ??= nameof(value);
                throw new ArgumentNullException(parameterName);
            }
        }

        internal static void NotNull([NotNull] object? obj, string? parameterName)
        {
            if (obj is null)
            {
                parameterName ??= nameof(obj);
                throw new ArgumentNullException(parameterName);
            }
        }

        /// <exception cref="ArgumentException"/>
        /// <exception cref="ArgumentNullException"/>
        internal static void NotNullOrEmpty([NotNull] string? value, string? parameterName)
        {
            parameterName ??= nameof(value);
            if (value is null)
            {
                throw new ArgumentNullException(parameterName);
            }
            else if (string.Empty == value)
            {
                throw new ArgumentException($"'{parameterName}' cannot be an empty string.", parameterName);
            }
        }
    }
}
