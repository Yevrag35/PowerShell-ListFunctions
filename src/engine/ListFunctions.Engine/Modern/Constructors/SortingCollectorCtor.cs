namespace ListFunctions.Modern.Constructors;

/// <summary>
/// Creates <see cref="SortedSet{T}"/> instances with an element type and comparer chosen at run time.
/// </summary>
/// <remarks>
/// <para>
/// When no comparer is passed to the constructor, <see cref="GetComparer"/> chooses one by element type. A comparer
/// that is passed is used for every element type, so it must be an <see cref="IComparer{T}"/> of the element type, or
/// <see cref="GenericCollectionCtor.Construct"/> throws.
/// </para>
/// <para>
/// Instances aren't thread-safe. The object caches its comparer the first time it needs one.
/// </para>
/// </remarks>
internal sealed class SortingCollectorCtor : GenericCollectionCtor
{
	/// <summary>
	/// The generic type definition of the set, <see cref="SortedSet{T}"/>.
	/// </summary>
	public static readonly Type SortedSetTypeDefinition = typeof(SortedSet<>);

	private IComparer? _comparer;
	private readonly Type _sortedType;

	/// <summary>
	/// Gets or sets a value that indicates whether the set's default string comparisons consider case.
	/// </summary>
	/// <remarks>
	/// This property has no effect when a comparer is passed to the constructor, or after the object caches a
	/// comparer. Set it before calling <see cref="GenericCollectionCtor.Construct"/> or <see cref="GetComparer"/>.
	/// </remarks>
	/// <value>
	/// <see langword="true"/> to compare strings with case; <see langword="false"/> to ignore case. Defaults to
	/// <see langword="false"/>.
	/// </value>
	public bool IsCaseSensitive { get; set; }

	/// <summary>
	/// Initializes a new <see cref="SortingCollectorCtor"/> instance with the specified element type and comparer.
	/// </summary>
	/// <param name="genericType">The element type of the set, or <see langword="null"/> for <see cref="object"/>.</param>
	/// <param name="comparer">The comparer for the set, or <see langword="null"/> to use a default comparer.</param>
	public SortingCollectorCtor(Type? genericType, IComparer? comparer)
		: base(SortedSetTypeDefinition, ToTypeArray(ref genericType))
	{
		_comparer = comparer;
		_sortedType = genericType;
	}

	/// <summary>
	/// Returns the comparer for the set.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When no comparer was passed to the constructor, the method chooses one by element type. For
	/// <see cref="string"/>, it returns <see cref="StringComparer.OrdinalIgnoreCase"/>, or
	/// <see cref="StringComparer.Ordinal"/> when <see cref="IsCaseSensitive"/> is <see langword="true"/>, and caches it.
	/// For any other type, it returns <see cref="Comparer{T}.Default"/> without caching it.
	/// </para>
	/// <para>
	/// <see cref="StringComparer.OrdinalIgnoreCase"/> orders strings as if they were uppercase, by the numeric values of
	/// their characters, so the order is the same in every culture. For example, <c>_</c> and letters outside ASCII,
	/// such as an accented <c>e</c>, sort after <c>Z</c>.
	/// </para>
	/// </remarks>
	/// <returns>The comparer passed to the constructor, or a default comparer for the element type.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the default comparer for the element type can't be retrieved.
	/// </exception>
	public IComparer GetComparer()
	{
		if (null != _comparer)
		{
			return _comparer;
		}
		else if (typeof(string).Equals(_sortedType))
		{
			_comparer = this.IsCaseSensitive
				? StringComparer.Ordinal
				: StringComparer.OrdinalIgnoreCase;

			return _comparer;
		}

		var genMeth = _getComparerMethod.Value.MakeGenericMethod(_sortedType);
		return genMeth.Invoke(null, null) as IComparer
			?? throw new InvalidOperationException("Failed to get default comparer.");
	}
	/// <summary>
	/// Returns the arguments for the <see cref="SortedSet{T}"/> constructor that takes a comparer.
	/// </summary>
	/// <param name="genericTypes">The generic type arguments of the set. This implementation doesn't use them.</param>
	/// <returns>The comparer from <see cref="GetComparer"/>.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the default comparer for the element type can't be retrieved.
	/// </exception>
	protected override IEnumerable<object?>? GetConstructorArguments(Type[] genericTypes)
	{
		yield return this.GetComparer();
	}
	/// <summary>
	/// Replaces a <see langword="null"/> element type with <see cref="object"/> and wraps it in a single-element array.
	/// </summary>
	/// <param name="genericType">
	/// The element type. When it's <see langword="null"/>, the method sets it to <see cref="object"/>.
	/// </param>
	/// <returns>An array that contains <paramref name="genericType"/> after the replacement.</returns>
	private static Type[] ToTypeArray([NotNull] ref Type? genericType)
	{
		genericType ??= typeof(object);
		return new Type[] { genericType };
	}
	private static readonly Lazy<MethodInfo> _getComparerMethod = new Lazy<MethodInfo>(InitializeLazyMethod);
	/// <summary>
	/// Returns the default comparer for <typeparamref name="T"/> as a non-generic <see cref="IComparer"/>.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <returns><see cref="Comparer{T}.Default"/>.</returns>
	private static IComparer GetDefaultComparer<T>()
	{
		return Comparer<T>.Default;
	}
	/// <summary>
	/// Gets the generic method definition of <see cref="GetDefaultComparer{T}"/>.
	/// </summary>
	/// <remarks>
	/// The method reads the definition from an expression tree instead of looking it up by name, so a rename of
	/// <see cref="GetDefaultComparer{T}"/> can't break it.
	/// </remarks>
	/// <returns>The open generic method definition, ready for <see cref="MethodInfo.MakeGenericMethod(Type[])"/>.</returns>
	private static MethodInfo InitializeLazyMethod()
	{
		Expression<Action> action = () => GetDefaultComparer<object>();
		MethodInfo m = ((MethodCallExpression)action.Body).Method;

		return m.GetGenericMethodDefinition();
	}
}

