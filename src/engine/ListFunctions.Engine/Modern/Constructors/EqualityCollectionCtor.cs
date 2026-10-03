namespace ListFunctions.Modern.Constructors;

/// <summary>
/// Provides the base class for objects that create generic collections whose constructors take a capacity and an
/// equality comparer.
/// </summary>
/// <remarks>
/// <para>
/// When no comparer is passed to the constructor, the collection uses a default comparer for the type that the
/// derived class uses for equality. String comparisons ignore case unless <see cref="IsCaseSensitive"/> is
/// <see langword="true"/>.
/// </para>
/// <para>
/// A comparer that is passed doesn't have to be an <see cref="IEqualityComparer{T}"/> of that type. Any other
/// <see cref="IEqualityComparer"/>, such as an <see cref="EqualityBlock"/> for a collection of <see cref="int"/>, is
/// wrapped in an <see cref="EqualityComparerAdapter{T}"/>.
/// </para>
/// <para>
/// Instances aren't thread-safe. The object caches the default comparer the first time it needs one.
/// </para>
/// </remarks>
public abstract class EqualityCollectionCtor : GenericCollectionCtor
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
	/// equality comparer, type arguments, and callback that closes the definition over the arguments.
	/// </summary>
	/// <param name="genericTypeDefinition">
	/// The open generic type definition of the collection. It must be a generic class that isn't abstract, and it
	/// must not be <see langword="null"/>.
	/// </param>
	/// <param name="comparer">
	/// The equality comparer for the collection, or <see langword="null"/> to use a default comparer.
	/// </param>
	/// <param name="genericTypes">The type arguments to close <paramref name="genericTypeDefinition"/> over.</param>
	/// <param name="createConstructingCallback">
	/// The method that closes <paramref name="genericTypeDefinition"/> over <paramref name="genericTypes"/>, or
	/// <see langword="null"/> to call <see cref="Type.MakeGenericType(Type[])"/>.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="genericTypeDefinition"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="genericTypeDefinition"/> isn't a generic type, isn't a class, or is abstract; or
	/// when <paramref name="createConstructingCallback"/> is null and <paramref name="genericTypes"/> doesn't satisfy
	/// the definition's type parameters.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="createConstructingCallback"/> is null and <paramref name="genericTypeDefinition"/>
	/// is a closed generic type instead of a generic type definition.
	/// </exception>
	protected EqualityCollectionCtor(Type genericTypeDefinition, IEqualityComparer? comparer, Type[] genericTypes, CreateConstructingType? createConstructingCallback)
		: base(genericTypeDefinition, genericTypes, createConstructingCallback)
	{
		_comparer = comparer;
	}

	/// <summary>
	/// Creates the fallback collection with the equality comparer that the collection would otherwise use.
	/// </summary>
	/// <remarks>
	/// This method passes the comparer that <see cref="GetConstructorArguments(Type[])"/> describes to
	/// <see cref="ConstructDefault(IEqualityComparer)"/>.
	/// </remarks>
	/// <returns>The new, empty collection.</returns>
	protected sealed override object ConstructDefault()
	{
		return this.ConstructDefault(this.GetComparerOrDefault());
	}
	/// <summary>
	/// Creates the fallback collection with the specified equality comparer.
	/// </summary>
	/// <param name="comparer">
	/// The comparer passed to the constructor, or the default comparer when that was <see langword="null"/>. An
	/// implementation can ignore it.
	/// </param>
	/// <returns>The new, empty collection. This value must not be <see langword="null"/>.</returns>
	protected abstract object ConstructDefault(IEqualityComparer comparer);
	/// <summary>
	/// Returns the comparer passed to the constructor, or a default comparer for the type used for equality.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The comparer passed to the constructor goes through <see cref="AdaptComparer(IEqualityComparer, Type)"/>, so the
	/// result is always an <see cref="IEqualityComparer{T}"/> of the type used for equality.
	/// </para>
	/// <para>
	/// For <see cref="string"/>, the method returns <see cref="StringComparer.InvariantCultureIgnoreCase"/>, or
	/// <see cref="StringComparer.InvariantCulture"/> when <see cref="IsCaseSensitive"/> is <see langword="true"/>, and
	/// reads <see cref="IsCaseSensitive"/> on every call. For any other type, it returns
	/// <see cref="EqualityComparer{T}.Default"/> and caches it in place of the constructor's comparer.
	/// </para>
	/// </remarks>
	/// <returns>The equality comparer for the collection.</returns>
	private IEqualityComparer GetComparerOrDefault()
	{
		if (_comparer is not null)
		{
			return AdaptComparer(_comparer, this.GetTypeForEquality());
		}

		Type equalityType = this.GetTypeForEquality();
		if (typeof(string).Equals(equalityType))
		{
			return !this.IsCaseSensitive
				? StringComparer.InvariantCultureIgnoreCase
				: StringComparer.InvariantCulture;
		}

		var genStaticType = DefaultComparerTypeDefinition.MakeGenericType(equalityType);

		PropertyInfo? defaultProp = genStaticType.GetProperty(
			nameof(EqualityComparer<>.Default), BindingFlags.Static | BindingFlags.Public);

		_comparer = (IEqualityComparer)defaultProp?.GetValue(null)!;
		return _comparer;
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
	/// method uses <see cref="EqualityComparer{T}.Default"/> for the type used for equality. For <see cref="string"/>,
	/// it uses <see cref="StringComparer.InvariantCultureIgnoreCase"/> instead, or
	/// <see cref="StringComparer.InvariantCulture"/> when <see cref="IsCaseSensitive"/> is <see langword="true"/>.
	/// </para>
	/// <para>
	/// When the comparer passed to the constructor isn't an <see cref="IEqualityComparer{T}"/> of the type used for
	/// equality, such as an <see cref="EqualityBlock"/> for a value type, the method wraps it in an
	/// <see cref="EqualityComparerAdapter{T}"/>.
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

	/// <summary>
	/// Determines whether <see cref="GenericCollectionCtor.Construct"/> creates the fallback collection.
	/// </summary>
	/// <remarks>
	/// This method passes the comparer from the constructor to
	/// <see cref="ShouldConstructDefault(IEqualityComparer, Type[])"/>.
	/// </remarks>
	/// <param name="genericTypes">The generic type arguments of the collection.</param>
	/// <returns>
	/// <see langword="true"/> to create the fallback collection; otherwise, <see langword="false"/>.
	/// </returns>
	protected sealed override bool ShouldConstructDefault(Type[] genericTypes)
	{
		return this.ShouldConstructDefault(_comparer, genericTypes);
	}
	/// <summary>
	/// Determines whether <see cref="GenericCollectionCtor.Construct"/> creates the fallback collection, given the
	/// comparer passed to the constructor.
	/// </summary>
	/// <param name="comparer">The comparer passed to the constructor, or <see langword="null"/> if none was passed.</param>
	/// <param name="genericTypes">The generic type arguments of the collection.</param>
	/// <returns>
	/// <see langword="true"/> to create the fallback collection; otherwise, <see langword="false"/>. This
	/// implementation returns <see langword="true"/> only when <paramref name="genericTypes"/> is empty.
	/// </returns>
	protected virtual bool ShouldConstructDefault(IEqualityComparer? comparer, Type[] genericTypes)
	{
		return genericTypes.Length <= 0;
	}
}

/// <summary>
/// Provides the base class for objects that create generic collections with an equality comparer and fall back to a
/// collection of a specific type.
/// </summary>
/// <remarks>
/// Because <typeparamref name="TDefault"/> is constrained to reference types, the fallback collection is returned
/// without boxing.
/// </remarks>
/// <typeparam name="TDefault">The type of the fallback collection.</typeparam>
public abstract class EqualityCollectionCtor<TDefault> : EqualityCollectionCtor where TDefault : class
{
	/// <summary>
	/// Initializes a new <see cref="EqualityCollectionCtor{TDefault}"/> instance with the specified generic type
	/// definition, equality comparer, type arguments, and callback that closes the definition over the arguments.
	/// </summary>
	/// <param name="genericTypeDefinition">
	/// The open generic type definition of the collection. It must be a generic class that isn't abstract, and it
	/// must not be <see langword="null"/>.
	/// </param>
	/// <param name="comparer">
	/// The equality comparer for the collection, or <see langword="null"/> to use a default comparer.
	/// </param>
	/// <param name="genericTypes">The type arguments to close <paramref name="genericTypeDefinition"/> over.</param>
	/// <param name="callback">
	/// The method that closes <paramref name="genericTypeDefinition"/> over <paramref name="genericTypes"/>, or
	/// <see langword="null"/> to call <see cref="Type.MakeGenericType(Type[])"/>.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="genericTypeDefinition"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="genericTypeDefinition"/> isn't a generic type, isn't a class, or is abstract; or
	/// when <paramref name="callback"/> is null and <paramref name="genericTypes"/> doesn't satisfy the definition's
	/// type parameters.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="callback"/> is null and <paramref name="genericTypeDefinition"/> is a closed generic
	/// type instead of a generic type definition.
	/// </exception>
	protected EqualityCollectionCtor(Type genericTypeDefinition, IEqualityComparer? comparer, Type[] genericTypes, CreateConstructingType? callback)
		: base(genericTypeDefinition, comparer, genericTypes, callback)
	{
	}

	/// <summary>
	/// Creates the fallback collection by calling <see cref="ConstructTDefault(IEqualityComparer)"/>.
	/// </summary>
	/// <param name="comparer">
	/// The comparer passed to the constructor, or the default comparer when that was <see langword="null"/>.
	/// </param>
	/// <returns>The new, empty collection.</returns>
	protected sealed override object ConstructDefault(IEqualityComparer comparer)
	{
		return this.ConstructTDefault(comparer);
	}
	/// <summary>
	/// Creates the fallback collection as a <typeparamref name="TDefault"/>.
	/// </summary>
	/// <param name="comparer">
	/// The comparer passed to the constructor, or the default comparer when that was <see langword="null"/>. An
	/// implementation can ignore it.
	/// </param>
	/// <returns>The new, empty collection. This value must not be <see langword="null"/>.</returns>
	protected abstract TDefault ConstructTDefault(IEqualityComparer comparer);
}

