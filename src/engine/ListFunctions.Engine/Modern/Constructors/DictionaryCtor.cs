namespace ListFunctions.Modern.Constructors;

/// <summary>
/// Creates <see cref="Dictionary{TKey, TValue}"/> instances with key and value types and an equality comparer chosen
/// at run time.
/// </summary>
/// <remarks>
/// <para>
/// When both the key and value types are <see cref="object"/> and the comparer isn't an
/// <see cref="IEqualityBlock"/>, <see cref="GenericCollectionCtor.Construct"/> creates a <see cref="Hashtable"/>
/// instead, like PowerShell's <c>@{}</c> literal. In that case, any other comparer passed to the constructor is
/// ignored.
/// </para>
/// <para>
/// Without a comparer, <see cref="object"/> keys compare the same way whatever the value type is: string keys without
/// regard to case unless <see cref="EqualityCollectionCtor.IsCaseSensitive"/> is <see langword="true"/>, and other keys
/// with their own <see cref="object.Equals(object)"/> method.
/// </para>
/// </remarks>
public sealed class DictionaryCtor : EqualityCollectionCtor<Hashtable>
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
	/// Gets the value type of the dictionary.
	/// </summary>
	/// <value>The value type passed to the constructor, or <see cref="object"/> when none was passed.</value>
	public Type ValueType { get; }

	/// <summary>
	/// Initializes a new <see cref="DictionaryCtor"/> instance with the specified key comparer, key type, and value
	/// type.
	/// </summary>
	/// <param name="comparer">
	/// The equality comparer for the keys, or <see langword="null"/> to use a default comparer.
	/// </param>
	/// <param name="keyType">The key type of the dictionary, or <see langword="null"/> for <see cref="object"/>.</param>
	/// <param name="valueType">The value type of the dictionary, or <see langword="null"/> for <see cref="object"/>.</param>
	public DictionaryCtor(IEqualityComparer? comparer, Type? keyType, Type? valueType)
		: base(TypeDefinition, comparer, [SetTypeOrObject(ref keyType), SetTypeOrObject(ref valueType)], null)
	{
		this.KeyType = keyType;
		this.ValueType = valueType;
	}

	/// <summary>
	/// Creates a <see cref="Hashtable"/> whose string keys compare without regard to case unless
	/// <see cref="EqualityCollectionCtor.IsCaseSensitive"/> is <see langword="true"/>.
	/// </summary>
	/// <remarks>
	/// String keys compare with <see cref="StringComparer.OrdinalIgnoreCase"/>, or with
	/// <see cref="StringComparer.CurrentCulture"/> when <see cref="EqualityCollectionCtor.IsCaseSensitive"/> is
	/// <see langword="true"/>. The table has room for <see cref="EqualityCollectionCtor.Capacity"/> entries.
	/// </remarks>
	/// <param name="comparer">The comparer that the base class chose. This implementation uses a string comparer instead.</param>
	/// <returns>The new, empty table.</returns>
	protected override Hashtable ConstructTDefault(IEqualityComparer comparer)
	{
		return new Hashtable(this.Capacity, this.GetObjectKeyComparer());
	}
	/// <summary>
	/// Returns the comparer for the keys when no comparer is passed to the constructor.
	/// </summary>
	/// <remarks>
	/// Whatever the value type is, <see cref="object"/> keys compare the way the keys of the <see cref="Hashtable"/> do,
	/// which <see cref="GenericCollectionCtor.Construct"/> creates when both types are <see cref="object"/>: strings with
	/// <see cref="StringComparer.OrdinalIgnoreCase"/>, or with <see cref="StringComparer.CurrentCulture"/> when
	/// <see cref="EqualityCollectionCtor.IsCaseSensitive"/> is <see langword="true"/>, and other keys with their own
	/// <see cref="object.Equals(object)"/> method. Other key types use the base implementation.
	/// </remarks>
	/// <param name="equalityType">The key type.</param>
	/// <returns>
	/// The string comparer for <see cref="object"/> keys; otherwise, the comparer from the base implementation, or
	/// <see langword="null"/>.
	/// </returns>
	protected override IEqualityComparer? GetDefaultComparer(Type equalityType)
	{
		return typeof(object).Equals(equalityType)
			? this.GetObjectKeyComparer()
			: base.GetDefaultComparer(equalityType);
	}
	/// <summary>
	/// Returns the string comparer that <see cref="object"/> keys compare with when no comparer is passed to the
	/// constructor.
	/// </summary>
	/// <remarks>
	/// The comparer compares two strings as strings and any other two keys with <see cref="object.Equals(object)"/>, so
	/// <c>1</c> and <c>"1"</c> are different keys.
	/// </remarks>
	/// <returns>
	/// <see cref="StringComparer.CurrentCulture"/> when <see cref="EqualityCollectionCtor.IsCaseSensitive"/> is
	/// <see langword="true"/>; otherwise, <see cref="StringComparer.OrdinalIgnoreCase"/>.
	/// </returns>
	private StringComparer GetObjectKeyComparer()
	{
		return this.IsCaseSensitive
			? StringComparer.CurrentCulture
			: StringComparer.OrdinalIgnoreCase;
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
	/// <summary>
	/// Determines whether <see cref="GenericCollectionCtor.Construct"/> creates a <see cref="Hashtable"/> instead of a
	/// <see cref="Dictionary{TKey, TValue}"/>.
	/// </summary>
	/// <param name="comparer">The comparer passed to the constructor, or <see langword="null"/> if none was passed.</param>
	/// <param name="genericTypes">The key and value types of the dictionary.</param>
	/// <returns>
	/// <see langword="true"/> when every type in <paramref name="genericTypes"/> is <see cref="object"/> and
	/// <paramref name="comparer"/> isn't an <see cref="IEqualityBlock"/>, or when the base class returns
	/// <see langword="true"/>; otherwise, <see langword="false"/>.
	/// </returns>
	protected override bool ShouldConstructDefault(IEqualityComparer? comparer, Type[] genericTypes)
	{
		return base.ShouldConstructDefault(comparer, genericTypes)
			   ||
			   (
					comparer is not IEqualityBlock
					&&
					genericTypes.All(x => typeof(object).Equals(x))
			   );
	}
}

