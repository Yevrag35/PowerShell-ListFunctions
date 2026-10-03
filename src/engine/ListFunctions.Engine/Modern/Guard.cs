namespace ListFunctions;

/// <summary>
/// Provides argument validation helpers that throw the standard argument exceptions on every target framework.
/// </summary>
/// <remarks>
/// <para>
/// On .NET 5 and later, each helper forwards to the matching static throw helper on <see cref="ArgumentNullException"/>,
/// <see cref="ArgumentException"/>, or <see cref="ArgumentOutOfRangeException"/>. On .NET Standard 2.0, each helper
/// performs the same check itself and throws the same exception type.
/// </para>
/// <para>
/// Every helper captures the caller's argument expression through <see cref="CallerArgumentExpressionAttribute"/>, so
/// the exception names the argument without the caller passing a name.
/// </para>
/// <para>All members are stateless and thread-safe.</para>
/// </remarks>
public static class Guard
{
	/// <summary>
	/// Holds the composite format for the message that <see cref="ThrowIfNegativeOrGreaterThan(int, uint, string?)"/> throws.
	/// </summary>
	/// <remarks>
	/// The placeholders are <c>{0}</c> for the parameter name, <c>{1}</c> for the actual value, and <c>{2}</c> for the
	/// inclusive upper bound. On .NET, the format is parsed once into a <c>CompositeFormat</c>; on .NET Standard 2.0,
	/// it stays a <see cref="string"/>.
	/// </remarks>
	private static readonly
#if NETCOREAPP
			CompositeFormat
#else
		string
#endif
			_outOfRange;

	/// <summary>
	/// Initializes the static state of the <see cref="Guard"/> class by preparing the out-of-range message format.
	/// </summary>
	static Guard()
	{
		string format = "{0} ('{1}') must not be negative but also not greater than '{2}'. (Parameter '{0}')\r\nActual value was {1}.";
#if NETCOREAPP
		_outOfRange = CompositeFormat.Parse(format);
#else
		_outOfRange = format;
#endif
	}

	/// <summary>
	/// Throws an <see cref="ArgumentNullException"/> when the specified object is <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// When this method returns, the compiler's nullable analysis treats <paramref name="obj"/> as not <see langword="null"/>.
	/// </remarks>
	/// <param name="obj">The object to validate. This value must not be <see langword="null"/>.</param>
	/// <param name="parameterName">
	/// The name to report in the exception. The compiler supplies the argument expression for <paramref name="obj"/> when this
	/// value is omitted.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is null.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
	/// <summary>
	/// Throws an <see cref="ArgumentNullException"/> when the specified pointer is a null pointer.
	/// </summary>
	/// <remarks>
	/// This method checks only the pointer value itself. It does not verify that a non-null pointer refers to valid or
	/// accessible memory.
	/// </remarks>
	/// <param name="argument">The pointer to validate. This value must not be a null pointer.</param>
	/// <param name="paramName">
	/// The name to report in the exception. The compiler supplies the argument expression for <paramref name="argument"/>
	/// when this value is omitted.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="argument"/> is a null pointer.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
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

	/// <summary>
	/// Throws an exception when the specified string is <see langword="null"/> or empty.
	/// </summary>
	/// <remarks>
	/// A string that contains only white space passes this check. When this method returns, the compiler's nullable analysis
	/// treats <paramref name="value"/> as not <see langword="null"/>.
	/// </remarks>
	/// <param name="value">The string to validate. This value must not be <see langword="null"/> or empty.</param>
	/// <param name="parameterName">
	/// The name to report in the exception. The compiler supplies the argument expression for <paramref name="value"/> when
	/// this value is omitted.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is an empty string.</exception>
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

	/// <summary>
	/// Throws an <see cref="ArgumentOutOfRangeException"/> when the specified value is greater than or equal to another value.
	/// </summary>
	/// <remarks>
	/// This method does not check for negative values. Use
	/// <see cref="ThrowIfNegativeOrGreaterThan(int, uint, string?)"/> when <paramref name="value"/> must also be non-negative.
	/// </remarks>
	/// <param name="value">The value to validate. This value must be less than <paramref name="other"/>.</param>
	/// <param name="other">The exclusive upper bound for <paramref name="value"/>.</param>
	/// <param name="parameterName">
	/// The name to report in the exception. The compiler supplies the argument expression for <paramref name="value"/> when
	/// this value is omitted.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="value"/> is greater than or equal to <paramref name="other"/>.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
	/// <para><b>Performance:</b> The single unsigned comparison replaces two signed comparisons and their branches.</para>
	/// </remarks>
	/// <param name="value">The value to validate. This value must not be negative and must not exceed <paramref name="other"/>.</param>
	/// <param name="other">
	/// The inclusive upper bound for <paramref name="value"/>. This value must be greater than 0 and less than or equal to
	/// <see cref="int.MaxValue"/>; a debug build asserts this condition on .NET Standard 2.0.
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
#endif
	}
}
