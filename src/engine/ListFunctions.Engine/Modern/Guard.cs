namespace ListFunctions;

/// <summary>
/// Provides an argument range check that throws the standard <see cref="ArgumentOutOfRangeException"/> on every target
/// framework.
/// </summary>
/// <remarks>
/// <para>
/// On .NET, the check forwards to a static throw helper on <see cref="ArgumentOutOfRangeException"/>. On .NET Standard 2.0,
/// it performs the same check itself and throws the same exception type.
/// </para>
/// <para>
/// The check captures the caller's argument expression through <see cref="CallerArgumentExpressionAttribute"/>, so the
/// exception names the argument without the caller passing a name.
/// </para>
/// <para>All members are stateless and thread-safe.</para>
/// </remarks>
internal static class Guard
{
	/// <summary>
	/// Throws an <see cref="ArgumentOutOfRangeException"/> when the specified value is negative or greater than an inclusive
	/// upper bound.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method reinterprets <paramref name="value"/> as a <see cref="uint"/> before the comparison. Every negative
	/// <see cref="int"/> becomes a value greater than <see cref="int.MaxValue"/>, so a single unsigned comparison rejects both
	/// negative values and values above <paramref name="other"/>.
	/// </para>
	/// <para>
	/// On .NET, the exception message reports the reinterpreted unsigned value, so a <paramref name="value"/> of -4 appears as
	/// 4294967292. On .NET Standard 2.0, the exception reports the original signed value.
	/// </para>
	/// <para>
	/// <b>Performance:</b> The single unsigned comparison replaces two signed comparisons and their branches. On .NET Standard
	/// 2.0, the method formats the message and throws in a local function that is never inlined, so the inlined check stays
	/// small.
	/// </para>
	/// </remarks>
	/// <param name="value">The value to validate. This value must not be negative and must not exceed <paramref name="other"/>.</param>
	/// <param name="other">
	/// The inclusive upper bound for <paramref name="value"/>. This value must be less than or equal to
	/// <see cref="int.MaxValue"/>; a debug build asserts this condition on .NET Standard 2.0. A bound of 0 accepts only 0.
	/// </param>
	/// <param name="paramName">
	/// The name to report in the exception. The compiler supplies the argument expression for <paramref name="value"/> when
	/// this value is omitted.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="value"/> is negative or greater than <paramref name="other"/>.
	/// </exception>
#if NETCOREAPP
	[StackTraceHidden]
#endif
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ThrowIfNegativeOrGreaterThan(int value, uint other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
	{
#if NET5_0_OR_GREATER
		ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)value, other, paramName);
#else

		Debug.Assert(other <= int.MaxValue, "The other value should always be less than or equal to int.MaxValue.");
		if ((uint)value > other)
		{
			throwOutOfRange(value, other, paramName);
		}

		//u ('4294967292') must be less than or equal to '4'. (Parameter 'u')
		// Actual value was 4294967292.

		[DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
		static void throwOutOfRange(int value, uint other, string? paramName)
		{
			const string format = "{0} ('{1}') must not be negative but also not greater than '{2}'. (Parameter '{0}')\r\nActual value was {1}.";

			throw new ArgumentOutOfRangeException(paramName, value,
				message: string.Format(
					CultureInfo.CurrentCulture,
					format,
					paramName,
					value,
					other));
		}
#endif
	}
}
