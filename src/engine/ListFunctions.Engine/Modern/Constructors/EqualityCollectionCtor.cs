using System.Reflection;

namespace ListFunctions.Modern.Constructors;

public abstract class EqualityCollectionCtor : GenericCollectionCtor
{
	public static readonly Type DefaultComparerTypeDefinition = typeof(EqualityComparer<>);

	IEqualityComparer? _comparer;
	int _capacity;

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
	public bool IsCaseSensitive { get; set; }

	protected EqualityCollectionCtor(Type genericTypeDefinition, IEqualityComparer? comparer, Type[] genericTypes, CreateConstructingType? createConstructingCallback)
		: base(genericTypeDefinition, genericTypes, createConstructingCallback)
	{
		_comparer = comparer;
	}

	protected sealed override object ConstructDefault()
	{
		return this.ConstructDefault(this.GetComparerOrDefault());
	}
	protected abstract object ConstructDefault(IEqualityComparer comparer);
	private IEqualityComparer GetComparerOrDefault()
	{
		if (_comparer is not null)
		{
			return _comparer;
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
	/// Returns the arguments for the collection's constructor that takes a capacity and an equality comparer.
	/// </summary>
	/// <remarks>
	/// The comparer is the one passed to this object's constructor. When that comparer is <see langword="null"/>, the
	/// method uses <see cref="EqualityComparer{T}.Default"/> for the type used for equality. For <see cref="string"/>,
	/// it uses <see cref="StringComparer.InvariantCultureIgnoreCase"/> instead, or
	/// <see cref="StringComparer.InvariantCulture"/> when <see cref="IsCaseSensitive"/> is <see langword="true"/>.
	/// </remarks>
	/// <param name="genericTypes">The generic type arguments of the collection. This implementation doesn't use them.</param>
	/// <returns><see cref="Capacity"/>, followed by the equality comparer.</returns>
	protected override IEnumerable<object?>? GetConstructorArguments(Type[] genericTypes)
	{
		yield return this.Capacity;
		yield return this.GetComparerOrDefault();
	}
	protected abstract Type GetTypeForEquality();

	public static bool IsTypeObjectOrString(Type? type)
	{
		return null != type && (typeof(object).Equals(type) || typeof(string).Equals(type));
	}

	protected sealed override bool ShouldConstructDefault(Type[] genericTypes)
	{
		return this.ShouldConstructDefault(_comparer, genericTypes);
	}
	protected virtual bool ShouldConstructDefault(IEqualityComparer? comparer, Type[] genericTypes)
	{
		return genericTypes.Length <= 0;
	}
}

public abstract class EqualityCollectionCtor<TDefault> : EqualityCollectionCtor where TDefault : class
{
	protected EqualityCollectionCtor(Type genericTypeDefinition, IEqualityComparer? comparer, Type[] genericTypes, CreateConstructingType? callback)
		: base(genericTypeDefinition, comparer, genericTypes, callback)
	{
	}

	protected sealed override object ConstructDefault(IEqualityComparer comparer)
	{
		return this.ConstructTDefault(comparer);
	}
	protected abstract TDefault ConstructTDefault(IEqualityComparer comparer);
}

