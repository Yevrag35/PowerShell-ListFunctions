using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ListFunctions
{
    public static class Guard
    {
#if NET5_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void NotNull([NotNull] object? obj, string? parameterName)
        {
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(obj, parameterName);
#else
            if (obj is null)
            {
                parameterName ??= nameof(obj);
                throw new ArgumentNullException(parameterName);
            }
#endif
        }

        /// <exception cref="ArgumentException"/>
        /// <exception cref="ArgumentNullException"/>
#if NET5_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void NotNullOrEmpty([NotNull] string? value, string? parameterName)
        {
#if NET5_0_OR_GREATER
            ArgumentException.ThrowIfNullOrEmpty(value, parameterName);
#else
            parameterName ??= nameof(value);
            if (value is null)
            {
                throw new ArgumentNullException(parameterName);
            }
            else if (string.Empty == value)
            {
                throw new ArgumentException($"'{parameterName}' cannot be an empty string.", parameterName);
            }
#endif
        }

#if NET5_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void ThrowIfGreaterThanOrEqual(int value, int other, string? parameterName)
        {
#if NET5_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, other, parameterName);
#else
            if (value >= other)
            {
                parameterName ??= nameof(value);
                throw new ArgumentOutOfRangeException(parameterName, value, $"'{parameterName}' must be less than {other}.");
            }
#endif
        }
    }
}
