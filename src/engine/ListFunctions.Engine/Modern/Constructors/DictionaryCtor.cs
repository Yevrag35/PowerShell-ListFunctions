namespace ListFunctions.Modern.Constructors;

/// <summary>
/// Creates <see cref="Dictionary{TKey, TValue}"/> instances with key and value types and an equality comparer chosen
/// at run time.
/// </summary>
/// <remarks>
/// <para>
/// The object always creates a <see cref="Dictionary{TKey, TValue}"/>, even when both types are <see cref="object"/>.
/// </para>
/// <para>
/// Without a comparer, <see cref="string"/> keys compare with <see cref="StringComparer.OrdinalIgnoreCase"/>, or with
/// <see cref="StringComparer.Ordinal"/> when <see cref="EqualityCollectionCtor.IsCaseSensitive"/> is
/// <see langword="true"/>. <see cref="object"/> keys compare with the same comparer whatever the value type is: two
/// strings the same way as <see cref="string"/> keys, and any other two keys with their own
/// <see cref="object.Equals(object)"/> method, so <c>1</c> and <c>"1"</c> are different keys. Keys of any other type
/// compare with <see cref="EqualityComparer{T}.Default"/>.
/// </para>
/// <para>
/// A comparer that is passed to the constructor works with any key type. One that isn't an
/// <see cref="IEqualityComparer{T}"/> of the key type, such as a <see cref="StringComparer"/> for <see cref="object"/>
/// keys, is wrapped in an <see cref="EqualityComparerAdapter{T}"/>.
/// </para>
/// </remarks>
internal sealed class DictionaryCtor : EqualityCollectionCtor
{
	/// <summary>
	/// The generic type definition of the dictionary, <see cref="Dictionary{TKey, TValue}"/>.
	/// </summary>
	public static readonly Type TypeDefinition = typeof(Dictionary<,>);
	/// <summary>
	/// Gets the key type of the dictionary.
	/// </summary>
	/// <value>The key type passed to the constructor, or <see cref="object"/> when none was passed.</value>
	public Type KeyType { get; }

	/// <summary>
	/// Initializes a new <see cref="DictionaryCtor"/> instance with the specified key comparer, key type, and value
	/// type.
	/// </summary>
	/// <param name="comparer">
	/// The equality comparer for the keys, or <see langword="null"/> to use a default comparer.
	/// </param>
	/// <param name="keyType">The key type of the dictionary, or <see langword="null"/> for <see cref="object"/>.</param>
	/// <param name="valueType">The value type of the dictionary, or <see langword="null"/> for <see cref="object"/>.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="keyType"/> or <paramref name="valueType"/> can't be a type argument of <see cref="Dictionary{TKey, TValue}"/>, such as a pointer type.</exception>
	public DictionaryCtor(IEqualityComparer? comparer, Type? keyType, Type? valueType)
		: base(TypeDefinition, comparer, [SetTypeOrObject(ref keyType), SetTypeOrObject(ref valueType)])
	{
		this.KeyType = keyType;
	}

	/// <summary>
	/// Returns the key type of the dictionary.
	/// </summary>
	/// <returns>The value of <see cref="KeyType"/>.</returns>
	protected override Type GetTypeForEquality()
	{
		return this.KeyType;
	}
	/// <summary>
	/// Replaces a <see langword="null"/> type with <see cref="object"/> and returns the result.
	/// </summary>
	/// <param name="type">
	/// The type to check. When it's <see langword="null"/>, the method sets it to <see cref="object"/>.
	/// </param>
	/// <returns>The value of <paramref name="type"/> after the replacement.</returns>
	private static Type SetTypeOrObject([NotNull] ref Type? type)
	{
		type ??= typeof(object);
		return type;
	}
}
