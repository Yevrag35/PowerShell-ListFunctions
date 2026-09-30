namespace ListFunctions.Modern.Constructors;

/// <summary>
/// Creates <see cref="SortedSet{T}"/> instances with an element type and comparer chosen at run time.
/// </summary>
/// <remarks>
/// <para>
/// When no comparer is passed to the constructor, a set of <see cref="object"/> orders its elements the way
/// PowerShell's comparison operators do. A comparer that is passed is used for every element type, so it must be an
/// <see cref="IComparer{T}"/> of the element type, or <see cref="GenericCollectionCtor.Construct"/> throws.
/// </para>
/// <para>
/// Instances aren't thread-safe. The object caches its comparer the first time it needs one.
/// </para>
/// </remarks>
public sealed class SortingCollectorCtor : GenericCollectionCtor
{
	/// <summary>
	/// The generic type definition of the set, <see cref="SortedSet{T}"/>.
	/// </summary>
	public static readonly Type SortedSetTypeDefinition = typeof(SortedSet<>);

	IComparer? _comparer;
	readonly Type _sortedType;

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
		: base(SortedSetTypeDefinition, ToTypeArray(ref genericType), null)
	{
		_comparer = comparer;
		_sortedType = genericType;
	}

	/// <summary>
	/// Creates a <see cref="SortedSet{T}"/> of <see cref="object"/> that orders its elements the way PowerShell's
	/// comparison operators do.
	/// </summary>
	/// <remarks>
	/// Strings compare without regard to case unless <see cref="IsCaseSensitive"/> is <see langword="true"/>. The
	/// method caches the new comparer, so <see cref="GetComparer"/> returns it afterward.
	/// </remarks>
	/// <returns>The new, empty set.</returns>
	protected override object ConstructDefault()
	{
		var comparer = new ObjectComparer(!this.IsCaseSensitive);
		_comparer = comparer;

		return new SortedSet<object>(comparer);
	}
	/// <summary>
	/// Returns the comparer for the set.
	/// </summary>
	/// <remarks>
	/// When no comparer was passed to the constructor, the method chooses one by element type. For <see cref="object"/>,
	/// it returns a comparer that orders elements the way PowerShell's comparison operators do. For
	/// <see cref="string"/>, it returns <see cref="StringComparer.InvariantCultureIgnoreCase"/>, or
	/// <see cref="StringComparer.InvariantCulture"/> when <see cref="IsCaseSensitive"/> is <see langword="true"/>. The
	/// method caches the comparer for both of those types. For any other type, it returns
	/// <see cref="Comparer{T}.Default"/> without caching it.
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
		else if (typeof(object).Equals(_sortedType))
		{
			_comparer = new ObjectComparer(!this.IsCaseSensitive);
			return _comparer;
		}
		else if (typeof(string).Equals(_sortedType))
		{
			_comparer = this.IsCaseSensitive
				? StringComparer.InvariantCulture
				: StringComparer.InvariantCultureIgnoreCase;

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
	/// Determines whether <see cref="GenericCollectionCtor.Construct"/> creates the PowerShell-style set of
	/// <see cref="object"/>.
	/// </summary>
	/// <remarks>
	/// When a comparer was passed to the constructor or is already cached, the method returns
	/// <see langword="false"/>, so <see cref="GenericCollectionCtor.Construct"/> creates the set with the comparer
	/// that <see cref="GetComparer"/> returns.
	/// </remarks>
	/// <param name="genericTypes">The generic type arguments of the set. This implementation doesn't use them.</param>
	/// <returns>
	/// <see langword="true"/> when the element type is <see cref="object"/>, no comparer was passed to the constructor,
	/// and none is cached yet; otherwise, <see langword="false"/>.
	/// </returns>
	protected override bool ShouldConstructDefault(Type[] genericTypes)
	{
		return typeof(object).Equals(_sortedType)
			   &&
			   _comparer is null;
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
	/// <summary>
	/// Compares objects the way PowerShell's comparison operators do.
	/// </summary>
	private sealed class ObjectComparer : IComparer<object?>, IComparer
	{
		readonly bool _ignoreCase;
		/// <summary>
		/// Gets a value that indicates whether string comparisons consider case.
		/// </summary>
		/// <value><see langword="true"/> if string comparisons consider case; otherwise, <see langword="false"/>.</value>
		internal bool IsCaseSensitive => !_ignoreCase;

		/// <summary>
		/// Initializes a new <see cref="ObjectComparer"/> instance that does or doesn't ignore case in string
		/// comparisons.
		/// </summary>
		/// <param name="ignoreCase"><see langword="true"/> to ignore case in string comparisons; otherwise, <see langword="false"/>.</param>
		internal ObjectComparer(bool ignoreCase)
		{
			_ignoreCase = ignoreCase;
		}

		/// <summary>
		/// Compares two objects by PowerShell's rules and returns a value that indicates their relative order.
		/// </summary>
		/// <remarks>
		/// This method calls <see cref="LanguagePrimitives.Compare(object, object, bool)"/>, which converts
		/// <paramref name="y"/> to the type of <paramref name="x"/> when their types differ.
		/// </remarks>
		/// <param name="x">The first object to compare, or <see langword="null"/>.</param>
		/// <param name="y">The second object to compare, or <see langword="null"/>.</param>
		/// <returns>
		/// A negative number if <paramref name="x"/> precedes <paramref name="y"/>, 0 if they're equal, or a positive
		/// number if <paramref name="x"/> follows <paramref name="y"/>.
		/// </returns>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="y"/> can't be converted to the type of <paramref name="x"/>, or when
		/// <paramref name="x"/> doesn't implement <see cref="IComparable"/>.
		/// </exception>
		public int Compare(object? x, object? y)
		{
			return LanguagePrimitives.Compare(x, y, _ignoreCase);
		}
	}

	static readonly Lazy<MethodInfo> _getComparerMethod = new Lazy<MethodInfo>(InitializeLazyMethod);
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

