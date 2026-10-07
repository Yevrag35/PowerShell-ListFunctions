namespace ListFunctions.Modern.Constructors;

/// <summary>
/// Creates <see cref="HashSet{T}"/> instances with an element type and equality comparer chosen at run time.
/// </summary>
/// <remarks>
/// Without a comparer, <see cref="string"/> elements compare with <see cref="StringComparer.OrdinalIgnoreCase"/>, or
/// with <see cref="StringComparer.Ordinal"/> when <see cref="EqualityCollectionCtor.IsCaseSensitive"/> is
/// <see langword="true"/>. <see cref="object"/> elements compare with the same comparer: two strings the same way as
/// <see cref="string"/> elements, and any other two elements with their own <see cref="object.Equals(object)"/> and
/// <see cref="object.GetHashCode"/> methods, so <c>1</c> and <c>"1"</c> are different elements. Elements of any other
/// type compare with <see cref="EqualityComparer{T}.Default"/>.
/// </remarks>
internal sealed class HashSetCtor : EqualityCollectionCtor
{
	/// <summary>
	/// The generic type definition of the set, <see cref="HashSet{T}"/>.
	/// </summary>
	public static readonly Type HashSetTypeDefinition = typeof(HashSet<>);
	private readonly Type _equalityType;

	/// <summary>
	/// Initializes a new <see cref="HashSetCtor"/> instance with the specified element type and equality comparer.
	/// </summary>
	/// <param name="genericType">The element type of the set, or <see langword="null"/> for <see cref="object"/>.</param>
	/// <param name="equalityComparer">
	/// The equality comparer for the set, or <see langword="null"/> to use a default comparer.
	/// </param>
	public HashSetCtor(Type? genericType, IEqualityComparer? equalityComparer)
		: base(HashSetTypeDefinition, equalityComparer, ToArrayOrEmpty(genericType), null)
	{
		_equalityType = genericType ?? typeof(object);
	}
	/// <summary>
	/// Wraps the specified element type in a single-element array.
	/// </summary>
	/// <param name="genericType">The element type, or <see langword="null"/> for <see cref="object"/>.</param>
	/// <returns>
	/// An array that contains <paramref name="genericType"/>, or <see cref="object"/> when
	/// <paramref name="genericType"/> is <see langword="null"/>. The array is never empty.
	/// </returns>
	private static Type[] ToArrayOrEmpty(Type? genericType)
	{
		genericType ??= typeof(object);

		return [genericType];
	}
	/// <summary>
	/// Returns the element type of the set.
	/// </summary>
	/// <returns>The element type passed to the constructor, or <see cref="object"/> when none was passed.</returns>
	protected sealed override Type GetTypeForEquality()
	{
		return _equalityType;
	}
}
