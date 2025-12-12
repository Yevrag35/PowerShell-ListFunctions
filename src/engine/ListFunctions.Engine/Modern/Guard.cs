using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace ListFunctions
{
    public static class Guard
    {
        private static readonly
#if NETCOREAPP
            CompositeFormat
#else
            string
#endif
                _outOfRange;

        static Guard()
        {
            string format = "{0} ('{1}') must not be negative but also not greater than '{2}'. (Parameter '{0}')\r\nActual value was {1}.";
#if NETCOREAPP
            _outOfRange = CompositeFormat.Parse(format);
#else
            _outOfRange = format;
#endif
        }

        public static void NotNull([NotNull] object? obj, [CallerArgumentExpression(nameof(obj))] string? parameterName = null)
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
        public static unsafe void NotNull([NotNull] void* argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(argument, paramName);
#else
            if (argument is null)
            {
                paramName ??= nameof(argument);
                throw new ArgumentNullException(paramName);
            }
#endif
        }

        /// <exception cref="ArgumentException"/>
        /// <exception cref="ArgumentNullException"/>
        public static void NotNullOrEmpty([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
#if NET5_0_OR_GREATER
            ArgumentException.ThrowIfNullOrEmpty(value, parameterName);
#else
            
            if (value is null)
            {
                parameterName ??= nameof(value);
                throw new ArgumentNullException(parameterName);
            }
            else if (string.Empty == value)
            {
                parameterName ??= nameof(value);
                throw new ArgumentException("The string cannot be empty.", parameterName);
            }
#endif
        }

        public static void ThrowIfGreaterThanOrEqual(int value, int other, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
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

        
        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> if the specified <paramref name="value"/> is negative  or
        /// greater than the specified <paramref name="other"/>.
        /// </summary>
        /// <remarks>This method is typically used to validate input parameters to ensure they fall within an acceptable
        /// range.</remarks>
        /// <param name="value">The integer value to validate. Must not be negative and must not exceed <paramref name="other"/>.</param>
        /// <param name="other">The upper limit, inclusive, that <paramref name="value"/> must not exceed. Must be less than or equal to <see
        /// cref="int.MaxValue"/>.</param>
        /// <param name="paramName">The name of the parameter being validated. This is automatically populated by the compiler if not explicitly
        /// provided.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="value"/> is negative or greater than <paramref name="other"/>.</exception>
#if NETCOREAPP
        [StackTraceHidden]
#endif
        public static void ThrowIfNegativeOrGreaterThan(int value, uint other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            Debug.Assert(other is <= int.MaxValue and not 0, "The other value should never be 0 and always less than or equal to int.MaxValue.");
            if ((uint)value > other)
            {
                throw new ArgumentOutOfRangeException(paramName, value,
                    message: string.Format(
                        CultureInfo.CurrentCulture,
                        _outOfRange,
                        paramName,
                        value,
                        other));
            }

            //u ('4294967292') must be less than or equal to '4'. (Parameter 'u')
            // Actual value was 4294967292.
        }
    }
}
