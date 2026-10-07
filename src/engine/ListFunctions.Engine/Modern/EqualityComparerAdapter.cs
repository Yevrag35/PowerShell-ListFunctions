namespace ListFunctions.Modern;

/// <summary>
/// Represents an equality comparer for a specific type that passes every comparison to a non-generic equality comparer.
/// </summary>
/// <remarks>
/// <para>
/// A generic collection, such as a <see cref="Dictionary{TKey, TValue}"/> or a <see cref="HashSet{T}"/>, takes an
/// <see cref="IEqualityComparer{T}"/> of its key or element type. An <see cref="EqualityBlock"/> is an
/// <see cref="IEqualityComparer{T}"/> of <see cref="object"/>, which a collection of a reference type, such as
/// <see cref="string"/>, accepts through contravariance. A collection of a value type, such as <see cref="int"/> or a
/// <see cref="Nullable{T}"/>, doesn't. This adapter lets such a collection use any <see cref="IEqualityComparer"/>.
/// </para>
/// <para>
/// When <typeparamref name="T"/> is a value type, the adapter boxes each value before it passes the value to the
/// inner comparer.
/// </para>
/// <para>
/// The adapter keeps no state of its own, so it's exactly as thread-safe as <see cref="InnerComparer"/>. An
/// <see cref="EqualityBlock"/> isn't thread-safe.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the objects to compare.</typeparam>
public sealed class EqualityComparerAdapter<T> : IEqualityComparer<T>, IEqualityComparer
{
	/// <summary>
	/// Gets the comparer that the adapter passes every comparison to.
	/// </summary>
	/// <value>The comparer passed to the constructor. It's never <see langword="null"/>.</value>
	public IEqualityComparer InnerComparer { get; }

	/// <summary>
	/// Initializes a new <see cref="EqualityComparerAdapter{T}"/> instance that passes every comparison to the specified
	/// comparer.
	/// </summary>
	/// <param name="innerComparer">The comparer that compares the objects. This value must not be <see langword="null"/>.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="innerComparer"/> is null.</exception>
	public EqualityComparerAdapter(IEqualityComparer innerComparer)
	{
		ArgumentNullException.ThrowIfNull(innerComparer);
		this.InnerComparer = innerComparer;
	}

	/// <summary>
	/// Determines whether the specified objects are equal by calling the inner comparer.
	/// </summary>
	/// <param name="x">The first object to compare, or <see langword="null"/>.</param>
	/// <param name="y">The second object to compare, or <see langword="null"/>.</param>
	/// <returns>The result of <see cref="IEqualityComparer.Equals(object, object)"/> on <see cref="InnerComparer"/>.</returns>
	public bool Equals(T? x, T? y)
	{
		return this.InnerComparer.Equals(x, y);
	}

	/// <summary>
	/// Returns a hash code for the specified object by calling the inner comparer.
	/// </summary>
	/// <param name="obj">The object to compute the hash code of. This value must not be <see langword="null"/>.</param>
	/// <returns>The result of <see cref="IEqualityComparer.GetHashCode(object)"/> on <see cref="InnerComparer"/>.</returns>
	public int GetHashCode(T obj)
	{
		return this.InnerComparer.GetHashCode(obj!);
	}

	/// <summary>
	/// Determines whether the specified objects are equal by calling the inner comparer.
	/// </summary>
	/// <remarks>
	/// The objects aren't converted to <typeparamref name="T"/> first.
	/// </remarks>
	/// <param name="x">The first object to compare, or <see langword="null"/>.</param>
	/// <param name="y">The second object to compare, or <see langword="null"/>.</param>
	/// <returns>The result of <see cref="IEqualityComparer.Equals(object, object)"/> on <see cref="InnerComparer"/>.</returns>
	bool IEqualityComparer.Equals(object? x, object? y)
	{
		return this.InnerComparer.Equals(x, y);
	}

	/// <summary>
	/// Returns a hash code for the specified object by calling the inner comparer.
	/// </summary>
	/// <remarks>
	/// The object isn't converted to <typeparamref name="T"/> first.
	/// </remarks>
	/// <param name="obj">The object to compute the hash code of. This value must not be <see langword="null"/>.</param>
	/// <returns>The result of <see cref="IEqualityComparer.GetHashCode(object)"/> on <see cref="InnerComparer"/>.</returns>
	int IEqualityComparer.GetHashCode(object obj)
	{
		return this.InnerComparer.GetHashCode(obj);
	}
}
