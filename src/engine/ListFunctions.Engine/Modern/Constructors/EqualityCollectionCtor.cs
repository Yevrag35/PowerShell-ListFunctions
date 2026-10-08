namespace ListFunctions.Modern.Constructors;

/// <summary>
/// Provides the base class for objects that create generic collections whose constructors take a capacity and an
/// equality comparer.
/// </summary>
/// <remarks>
/// <para>
/// When no comparer is passed to the constructor, the collection uses a default comparer for the type that the
/// derived class uses for equality. <see cref="string"/> values compare with
/// <see cref="StringComparer.OrdinalIgnoreCase"/>, or with <see cref="StringComparer.Ordinal"/> when
/// <see cref="IsCaseSensitive"/> is <see langword="true"/>. <see cref="object"/> values compare with the same
/// <see cref="StringComparer"/>, which compares two strings as strings, and any other two values with their own
/// <see cref="object.Equals(object)"/> and <see cref="object.GetHashCode"/> methods, so <c>1</c> and <c>"1"</c> aren't
/// equal. Values of any other type compare with <see cref="EqualityComparer{T}.Default"/>.
/// </para>
/// <para>
/// A comparer that is passed doesn't have to be an <see cref="IEqualityComparer{T}"/> of that type. Any other
/// <see cref="IEqualityComparer"/>, such as an <see cref="EqualityBlock"/> for a collection of <see cref="int"/>, or a
/// <see cref="StringComparer"/> for a collection of <see cref="object"/>, is wrapped in an
/// <see cref="EqualityComparerAdapter{T}"/>.
/// </para>
/// <para>
/// Instances aren't thread-safe. The object caches the default comparer the first time it needs one.
/// </para>
/// </remarks>
internal abstract class EqualityCollectionCtor : GenericCollectionCtor
{
	/// <summary>
	/// The generic type definition of the default equality comparer, <see cref="EqualityComparer{T}"/>.
	/// </summary>
	public static readonly Type DefaultComparerTypeDefinition = typeof(EqualityComparer<>);

	private IEqualityComparer? _comparer;
	private int _capacity;

	/// <summary>
	/// Gets or sets the initial capacity of the collection.
	/// </summary>
	/// <remarks>
	/// The collection is created with room for this many elements, so it doesn't have to grow until it holds more. A
	/// value of 0 creates the collection the same way a constructor without a capacity does.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
	/// <value>The number of elements the collection can hold before it has to grow. Defaults to 0.</value>
	public int Capacity
	{
		get => _capacity;
		set
		{
			Guard.ThrowIfNegativeOrGreaterThan(value, int.MaxValue);
			_capacity = value;
		}
	}
	/// <summary>
	/// Gets or sets a value that indicates whether the collection's default string comparisons consider case.
	/// </summary>
	/// <remarks>
	/// This property has no effect when a comparer is passed to the constructor. Set it before calling
	/// <see cref="GenericCollectionCtor.Construct"/>.
	/// </remarks>
	/// <value>
	/// <see langword="true"/> to compare strings with case; <see langword="false"/> to ignore case. Defaults to
	/// <see langword="false"/>.
	/// </value>
	public bool IsCaseSensitive { get; set; }

	/// <summary>
	/// Initializes a new <see cref="EqualityCollectionCtor"/> instance with the specified generic type definition,
	/// equality comparer, and type arguments.
	/// </summary>
	/// <param name="genericTypeDefinition">
	/// The open generic type definition of the collection. It must be a generic class that isn't abstract, and it
	/// must not be <see langword="null"/>.
	/// </param>
	/// <param name="comparer">
	/// The equality comparer for the collection, or <see langword="null"/> to use a default comparer.
	/// </param>
	/// <param name="genericTypes">The type arguments to close <paramref name="genericTypeDefinition"/> over.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="genericTypeDefinition"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="genericTypeDefinition"/> isn't a generic type, isn't a class, or is abstract; or
	/// when <paramref name="genericTypes"/> doesn't satisfy the definition's type parameters.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="genericTypeDefinition"/> is a closed generic type instead of a generic type
	/// definition.
	/// </exception>
	protected EqualityCollectionCtor(Type genericTypeDefinition, IEqualityComparer? comparer, Type[] genericTypes)
		: base(genericTypeDefinition, genericTypes)
	{
		_comparer = comparer;
	}

	/// <summary>
	/// Returns the comparer passed to the constructor, or a default comparer for the type used for equality.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The comparer passed to the constructor, and the one from <see cref="GetDefaultComparer(Type)"/>, go through
	/// <see cref="AdaptComparer(IEqualityComparer, Type)"/>, so the result is always an
	/// <see cref="IEqualityComparer{T}"/> of the type used for equality.
	/// </para>
	/// <para>
	/// When no comparer was passed, the method calls <see cref="GetDefaultComparer(Type)"/> on every call, so its result
	/// can depend on <see cref="IsCaseSensitive"/>. When that returns <see langword="null"/>, the method returns
	/// <see cref="EqualityComparer{T}.Default"/> and caches it in place of the constructor's comparer.
	/// </para>
	/// </remarks>
	/// <returns>The equality comparer for the collection.</returns>
	private IEqualityComparer GetComparerOrDefault()
	{
		Type equalityType = this.GetTypeForEquality();
		if (_comparer is not null)
		{
			return AdaptComparer(_comparer, equalityType);
		}

		if (this.GetDefaultComparer(equalityType) is { } defaultComparer)
		{
			return AdaptComparer(defaultComparer, equalityType);
		}

		var genStaticType = DefaultComparerTypeDefinition.MakeGenericType(equalityType);

		PropertyInfo? defaultProp = genStaticType.GetProperty(
			nameof(EqualityComparer<>.Default), BindingFlags.Static | BindingFlags.Public);

		_comparer = (IEqualityComparer)defaultProp?.GetValue(null)!;
		return _comparer;
	}
	/// <summary>
	/// Returns the comparer that the collection uses when no comparer is passed to the constructor, or
	/// <see langword="null"/> to use <see cref="EqualityComparer{T}.Default"/> of the type used for equality.
	/// </summary>
	/// <remarks>
	/// <para>
	/// For <see cref="string"/> and <see cref="object"/>, the method returns <see cref="StringComparer.OrdinalIgnoreCase"/>,
	/// or <see cref="StringComparer.Ordinal"/> when <see cref="IsCaseSensitive"/> is <see langword="true"/>. A
	/// <see cref="StringComparer"/> compares two strings as strings, and any other two objects with their own
	/// <see cref="object.Equals(object)"/> and <see cref="object.GetHashCode"/> methods. For any other type, the method
	/// returns <see langword="null"/>.
	/// </para>
	/// <para>
	/// The method runs each time the collection is created, so its result can depend on <see cref="IsCaseSensitive"/>.
	/// The comparer isn't an <see cref="IEqualityComparer{T}"/> of <see cref="object"/>, so for <see cref="object"/>,
	/// the caller wraps it in an <see cref="EqualityComparerAdapter{T}"/>.
	/// </para>
	/// </remarks>
	/// <param name="equalityType">The type that the collection compares for equality.</param>
	/// <returns>The default comparer, or <see langword="null"/> to use <see cref="EqualityComparer{T}.Default"/>.</returns>
	private StringComparer? GetDefaultComparer(Type equalityType)
	{
		if (!IsTypeObjectOrString(equalityType))
		{
			return null;
		}

		return this.IsCaseSensitive
			? StringComparer.Ordinal
			: StringComparer.OrdinalIgnoreCase;
	}
	/// <summary>
	/// Returns the specified comparer as an <see cref="IEqualityComparer{T}"/> of the specified type, and wraps it in an
	/// <see cref="EqualityComparerAdapter{T}"/> when it isn't one.
	/// </summary>
	/// <remarks>
	/// A comparer of a less derived type already qualifies through contravariance, so an <see cref="EqualityBlock"/>,
	/// which is an <see cref="IEqualityComparer{T}"/> of <see cref="object"/>, is returned as it is for any reference
	/// type. For a value type, such as <see cref="int"/> or a <see cref="Nullable{T}"/>, it's wrapped, because the
	/// collection's constructor wouldn't accept it.
	/// </remarks>
	/// <param name="comparer">The comparer to adapt. This value must not be <see langword="null"/>.</param>
	/// <param name="equalityType">The type that the collection compares for equality.</param>
	/// <returns><paramref name="comparer"/>, or an <see cref="EqualityComparerAdapter{T}"/> of <paramref name="equalityType"/> that wraps it.</returns>
	private static IEqualityComparer AdaptComparer(IEqualityComparer comparer, Type equalityType)
	{
		if (typeof(IEqualityComparer<>).MakeGenericType(equalityType).IsInstanceOfType(comparer))
		{
			return comparer;
		}

		Type adapterType = typeof(EqualityComparerAdapter<>).MakeGenericType(equalityType);
		return (IEqualityComparer)Activator.CreateInstance(adapterType, comparer)!;
	}
	/// <summary>
	/// Returns the arguments for the collection's constructor that takes a capacity and an equality comparer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The comparer is the one passed to this object's constructor. When that comparer is <see langword="null"/>, the
	/// method uses the default comparer for the type used for equality, as the class remarks describe.
	/// </para>
	/// <para>
	/// When the comparer isn't an <see cref="IEqualityComparer{T}"/> of the type used for equality, such as an
	/// <see cref="EqualityBlock"/> for a value type, or a <see cref="StringComparer"/> for <see cref="object"/>, the
	/// method wraps it in an <see cref="EqualityComparerAdapter{T}"/>.
	/// </para>
	/// </remarks>
	/// <param name="genericTypes">The generic type arguments of the collection. This implementation doesn't use them.</param>
	/// <returns><see cref="Capacity"/>, followed by the equality comparer.</returns>
	protected override IEnumerable<object?>? GetConstructorArguments(Type[] genericTypes)
	{
		yield return this.Capacity;
		yield return this.GetComparerOrDefault();
	}
	/// <summary>
	/// Returns the type whose values the collection compares for equality.
	/// </summary>
	/// <remarks>
	/// For a set, this is the element type. For a dictionary, it's the key type.
	/// </remarks>
	/// <returns>The type that the default equality comparer compares.</returns>
	protected abstract Type GetTypeForEquality();

	/// <summary>
	/// Determines whether the specified type is <see cref="object"/> or <see cref="string"/>.
	/// </summary>
	/// <param name="type">The type to check, or <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="type"/> is <see cref="object"/> or <see cref="string"/>; otherwise,
	/// <see langword="false"/>, including when <paramref name="type"/> is <see langword="null"/>.
	/// </returns>
	public static bool IsTypeObjectOrString(Type? type)
	{
		return null != type && (typeof(object).Equals(type) || typeof(string).Equals(type));
	}
}

