namespace ListFunctions.Extensions;

/// <summary>
/// Provides <see langword="null"/>-safe equality checks for types that implement <see cref="IEquatable{T}"/>.
/// </summary>
/// <remarks>
/// The methods call <see cref="IEquatable{T}.Equals(T)"/> directly, so value types are compared without boxing. They treat two
/// <see langword="null"/> values as equal, and a <see langword="null"/> value as unequal to any non-<see langword="null"/> value.
/// </remarks>
[DebuggerStepThrough]
internal static class EqualityExtensions
{
	/// <summary>
	/// Determines whether a value equals a nullable value of the same type.
	/// </summary>
	/// <typeparam name="T">The value type to compare.</typeparam>
	/// <param name="this">The value to compare.</param>
	/// <param name="other">The nullable value to compare with. This value can be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when <paramref name="other"/> has a value that equals <paramref name="this"/>; otherwise,
	/// <see langword="false"/>.
	/// </returns>
	public static bool IsEqualTo<T>(this T @this, [NotNullWhen(true)] T? other) where T : struct, IEquatable<T>
	{
		return other.HasValue && @this.Equals(other.Value);
	}
	/// <summary>
	/// Determines whether two values of the same type are equal, treating two <see langword="null"/> values as equal.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <param name="this">The value to compare. This value can be <see langword="null"/>.</param>
	/// <param name="other">The value to compare with. This value can be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when both values are <see langword="null"/>, or when <paramref name="this"/> is not
	/// <see langword="null"/> and equals <paramref name="other"/>; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsEqualTo<T>(this T @this, T other) where T : IEquatable<T>
	{
		return BothAreNull(@this, other)
			   ||
			   LeftIsEqualToRight(@this, other);
	}
	/// <summary>
	/// Determines whether a value equals a value of another type, treating two <see langword="null"/> values as equal.
	/// </summary>
	/// <typeparam name="T">The type to compare, which defines equality with <typeparamref name="TOther"/>.</typeparam>
	/// <typeparam name="TOther">The type of the value to compare with.</typeparam>
	/// <param name="this">The value to compare. This value can be <see langword="null"/>.</param>
	/// <param name="other">The value to compare with. This value can be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when both values are <see langword="null"/>, or when <paramref name="this"/> is not
	/// <see langword="null"/> and its <see cref="IEquatable{T}.Equals(T)"/> implementation returns <see langword="true"/> for
	/// <paramref name="other"/>; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsEqualTo<T, TOther>(this T @this, TOther other) where T : IEquatable<TOther>
	{
		return BothAreNull(@this, other)
			   ||
			   LeftIsEqualToRight(@this, other);
	}

	/// <summary>
	/// Determines whether both values are <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// For a non-nullable value type, the JIT compiler removes the <see langword="null"/> checks, and the method returns
	/// <see langword="false"/>.
	/// </remarks>
	/// <typeparam name="T1">The type of the first value.</typeparam>
	/// <typeparam name="T2">The type of the second value.</typeparam>
	/// <param name="t1">The first value.</param>
	/// <param name="t2">The second value.</param>
	/// <returns><see langword="true"/> when both <paramref name="t1"/> and <paramref name="t2"/> are <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
	private static bool BothAreNull<T1, T2>(T1 t1, T2 t2)
	{
		return t1 is null && t2 is null;
	}
	/// <summary>
	/// Determines whether a non-<see langword="null"/> left value equals the right value.
	/// </summary>
	/// <typeparam name="T1">The type of the left value, which defines equality with <typeparamref name="T2"/>.</typeparam>
	/// <typeparam name="T2">The type of the right value.</typeparam>
	/// <param name="t1">The left value. This value can be <see langword="null"/>.</param>
	/// <param name="t2">The right value. This value can be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when <paramref name="t1"/> is not <see langword="null"/> and equals <paramref name="t2"/>; otherwise,
	/// <see langword="false"/>.
	/// </returns>
	private static bool LeftIsEqualToRight<T1, T2>(T1 t1, T2 t2) where T1 : IEquatable<T2>
	{
		return t1 is not null && t1.Equals(t2);
	}
}
