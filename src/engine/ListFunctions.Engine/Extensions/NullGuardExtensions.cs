#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System;
#pragma warning restore IDE0130 // Namespace does not match folder structure

#if !NET6_0_OR_GREATER

/// <summary>
/// Provides the static argument-validation helpers that .NET declares on <see cref="ArgumentNullException"/>,
/// <see cref="ArgumentException"/>, and <see cref="ArgumentOutOfRangeException"/>, for targets that lack them.
/// </summary>
/// <remarks>
/// <para>
/// The class compiles only for targets other than .NET. Its static extension members let a call such as
/// <c>ArgumentNullException.ThrowIfNull(value)</c> compile unchanged on every target: the .NET build binds to the method
/// that .NET declares, and the .NET Standard 2.0 build binds to the member here. The class lives in the <c>System</c>
/// namespace, so the call needs no extra <see langword="using"/> directive.
/// </para>
/// <para>
/// The class is internal, so only this assembly and the assemblies that it grants access to its internals can call these
/// members. Those include the Windows PowerShell 5.1 module, which compiles the PowerShell 7 module's source files.
/// </para>
/// <para>
/// Every member captures the caller's argument expression through <see cref="CallerArgumentExpressionAttribute"/>, so the
/// exception names the argument without the caller passing a name.
/// </para>
/// <para>All members are stateless and thread-safe.</para>
/// <para>
/// <b>Performance:</b> Every member is marked for aggressive inlining. The members that format a message build and throw
/// the exception in a local function that is never inlined, so the inlined check stays small.
/// </para>
/// </remarks>
internal static class NullGuardExtensions
{
	/// <summary>
	/// Provides the static <see langword="null"/> check that .NET 6 and later declare on <see cref="ArgumentNullException"/>.
	/// </summary>
	extension(ArgumentNullException)
	{
		/// <summary>
		/// Throws an <see cref="ArgumentNullException"/> when the specified object is <see langword="null"/>.
		/// </summary>
		/// <remarks>
		/// When this method returns, the compiler's nullable analysis treats <paramref name="obj"/> as not <see langword="null"/>.
		/// </remarks>
		/// <param name="obj">The object to validate. This value must not be <see langword="null"/>.</param>
		/// <param name="paramName">
		/// The name to report in the exception. The compiler supplies the argument expression for <paramref name="obj"/> when
		/// this value is omitted. When the caller passes <see langword="null"/>, the exception reports <c>obj</c>.
		/// </param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is null.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void ThrowIfNull([NotNull] object? obj, [CallerArgumentExpression(nameof(obj))] string? paramName = null)
		{
			if (obj is null)
			{
				paramName ??= nameof(obj);
				throw new ArgumentNullException(paramName);
			}
		}
	}

	/// <summary>
	/// Provides the static string checks that .NET 7 and .NET 8 declare on <see cref="ArgumentException"/>.
	/// </summary>
	/// <remarks>
	/// Each member throws an <see cref="ArgumentNullException"/> for a <see langword="null"/> string and an
	/// <see cref="ArgumentException"/> for any other rejected string, as .NET does. The <see cref="ArgumentException"/>
	/// carries the parameter name only in its message, in the format .NET uses, so its <see cref="ArgumentException.ParamName"/>
	/// property is <see langword="null"/>.
	/// </remarks>
	extension(ArgumentException)
	{
		/// <summary>
		/// Throws an exception when the specified string is <see langword="null"/> or empty.
		/// </summary>
		/// <remarks>
		/// A string that contains only white space passes this check. When this method returns, the compiler's nullable analysis
		/// treats <paramref name="argument"/> as not <see langword="null"/>.
		/// </remarks>
		/// <param name="argument">The string to validate. This value must not be <see langword="null"/> or empty.</param>
		/// <param name="paramName">
		/// The name to report in the exception. The compiler supplies the argument expression for <paramref name="argument"/>
		/// when this value is omitted.
		/// </param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="argument"/> is null.</exception>
		/// <exception cref="ArgumentException">Thrown when <paramref name="argument"/> is an empty string.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void ThrowIfNullOrEmpty([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting.
		{
			if (string.IsNullOrEmpty(argument))
			{
				throwEmpty(argument, paramName);
			}

			[DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
			static void throwEmpty(string? argument, string? paramName)
			{
				const string message = "The value cannot be an empty string.";
				const string format = "{0} (Parameter '{1}')";

				string msg = message;
				if (!string.IsNullOrEmpty(paramName))
				{
					msg = string.Format(CultureInfo.InvariantCulture, format, message, paramName);
				}

				throw argument is null ? new ArgumentNullException(paramName) : new ArgumentException(msg);
			}
		}
#pragma warning restore CS8777

		/// <summary>
		/// Throws an exception when the specified string is <see langword="null"/>, empty, or contains only white space.
		/// </summary>
		/// <remarks>
		/// When this method returns, the compiler's nullable analysis treats <paramref name="argument"/> as not
		/// <see langword="null"/>.
		/// </remarks>
		/// <param name="argument">
		/// The string to validate. This value must not be <see langword="null"/>, empty, or only white space.
		/// </param>
		/// <param name="paramName">
		/// The name to report in the exception. The compiler supplies the argument expression for <paramref name="argument"/>
		/// when this value is omitted.
		/// </param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="argument"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="argument"/> is an empty string or contains only white space.
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void ThrowIfNullOrWhiteSpace([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting.
		{
			if (string.IsNullOrWhiteSpace(argument))
			{
				throwWhitespace(argument, paramName);
			}

			[DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
			static void throwWhitespace(string? argument, string? paramName)
			{
				const string message = "The value cannot be an empty string or composed entirely of whitespace.";
				const string format = "{0} (Parameter '{1}')";
				string msg = message;
				if (!string.IsNullOrEmpty(paramName))
				{
					msg = string.Format(CultureInfo.InvariantCulture, format, message, paramName);
				}

				throw argument is null ? new ArgumentNullException(paramName) : new ArgumentException(msg);
			}
		}
#pragma warning restore CS8777
	}

	/// <summary>
	/// Provides a static range check on <see cref="ArgumentOutOfRangeException"/> that rejects negative values and values
	/// above an inclusive upper bound.
	/// </summary>
	/// <remarks>
	/// Unlike the other members of this class, this one has no .NET counterpart, so a call to it compiles only for
	/// .NET Standard 2.0.
	/// </remarks>
	extension(ArgumentOutOfRangeException)
	{
		/// <summary>
		/// Throws an <see cref="ArgumentOutOfRangeException"/> when the specified value is negative or greater than an inclusive
		/// upper bound.
		/// </summary>
		/// <param name="value">
		/// The value to validate. This value must not be negative and must not exceed <paramref name="maxValue"/>.
		/// </param>
		/// <param name="maxValue">The inclusive upper bound for <paramref name="value"/>.</param>
		/// <param name="paramName">
		/// The name to report in the exception. The compiler supplies the argument expression for <paramref name="value"/> when
		/// this value is omitted. When the caller passes <see langword="null"/>, the exception reports <c>value</c>.
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Thrown when <paramref name="value"/> is negative or greater than <paramref name="maxValue"/>.
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void ThrowIfNegativeOrGreaterThan(int value, uint maxValue, [CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value < 0 || value > maxValue)
			{
				throwOutOfRange(paramName, value, maxValue);
			}

			[DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
			static void throwOutOfRange(string? paramName, int value, uint maxValue)
			{
				paramName ??= nameof(value);
				throw new ArgumentOutOfRangeException(paramName, value, $"The value must be non-negative and less than or equal to {maxValue}.");
			}
		}
	}
}
#endif