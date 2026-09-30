namespace ListFunctions.Modern.Constructors;

/// <summary>
/// Creates <see cref="HashSet{T}"/> instances with an element type and equality comparer chosen at run time.
/// </summary>
/// <remarks>
/// When the element type is <see cref="object"/> and no custom comparer is supplied,
/// <see cref="GenericCollectionCtor.Construct"/> creates a <see cref="HashSet{T}"/> of <see cref="object"/> that
/// compares its elements the way PowerShell's <c>-eq</c> operator does.
/// </remarks>
public sealed class HashSetCtor : EqualityCollectionCtor<HashSet<object>>
{
	/// <summary>
	/// The generic type definition of the set, <see cref="HashSet{T}"/>.
	/// </summary>
	public static readonly Type HashSetTypeDefinition = typeof(HashSet<>);
	readonly Type _equalityType;

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
	/// Creates a <see cref="HashSet{T}"/> of <see cref="object"/> that compares its elements the way PowerShell's
	/// <c>-eq</c> operator does.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Strings compare without regard to case unless <see cref="EqualityCollectionCtor.IsCaseSensitive"/> is
	/// <see langword="true"/>. The set has room for <see cref="EqualityCollectionCtor.Capacity"/> elements.
	/// </para>
	/// <para>
	/// .NET Standard 2.0 doesn't define the <see cref="HashSet{T}"/> constructor that takes a capacity and a comparer,
	/// so the .NET Standard 2.0 build calls it through reflection. .NET Framework 4.7.2 and later define it.
	/// </para>
	/// </remarks>
	/// <param name="comparer">The comparer that the base class chose. This implementation uses its own comparer instead.</param>
	/// <returns>The new, empty set.</returns>
	protected override HashSet<object> ConstructTDefault(IEqualityComparer comparer)
	{
		var objComparer = new ObjectEqualityComparer()
		{
			IgnoreCase = !this.IsCaseSensitive,
		};

#if NETSTANDARD2_0
		return (HashSet<object>)Activator.CreateInstance(typeof(HashSet<object>), this.Capacity, objComparer);
#else
		return new HashSet<object>(this.Capacity, objComparer);
#endif
	}
	/// <summary>
	/// Returns the element type of the set.
	/// </summary>
	/// <returns>The element type passed to the constructor, or <see cref="object"/> when none was passed.</returns>
	protected sealed override Type GetTypeForEquality()
	{
		return _equalityType;
	}
	/// <summary>
	/// Determines whether <see cref="GenericCollectionCtor.Construct"/> creates the PowerShell-style set of
	/// <see cref="object"/>.
	/// </summary>
	/// <param name="comparer">The comparer passed to the constructor, or <see langword="null"/> if none was passed.</param>
	/// <param name="genericTypes">The generic type arguments of the set.</param>
	/// <returns>
	/// <see langword="true"/> when the element type is <see cref="object"/> and <paramref name="comparer"/> is
	/// <see langword="null"/> or equals <see cref="EqualityComparer{T}.Default"/> for <see cref="object"/>, or when
	/// the base class returns <see langword="true"/>; otherwise, <see langword="false"/>.
	/// </returns>
	protected override bool ShouldConstructDefault(IEqualityComparer? comparer, Type[] genericTypes)
	{
		return (typeof(object).Equals(_equalityType) && (comparer is null || EqualityComparer<object>.Default.Equals(comparer)))
			   ||
			   base.ShouldConstructDefault(comparer, genericTypes);
	}

	/// <summary>
	/// Compares objects for equality the way PowerShell's <c>-eq</c> operator does.
	/// </summary>
	/// <remarks>
	/// Instances aren't thread-safe while <see cref="IgnoreCase"/> changes.
	/// </remarks>
	private sealed class ObjectEqualityComparer : IEqualityComparer<object?>, IEqualityComparer
	{
		/// <summary>
		/// Gets or sets a value that indicates whether string comparisons ignore case.
		/// </summary>
		/// <value>
		/// <see langword="true"/> to ignore case; otherwise, <see langword="false"/>. Defaults to
		/// <see langword="false"/>.
		/// </value>
		internal bool IgnoreCase { get; set; }

		/// <summary>
		/// Determines whether two objects are equal by PowerShell's rules.
		/// </summary>
		/// <remarks>
		/// This method calls <see cref="LanguagePrimitives.Equals(object, object, bool)"/>, which converts
		/// <paramref name="y"/> to the type of <paramref name="x"/> when their types differ.
		/// </remarks>
		/// <param name="x">The first object to compare, or <see langword="null"/>.</param>
		/// <param name="y">The second object to compare, or <see langword="null"/>.</param>
		/// <returns>
		/// <see langword="true"/> if <paramref name="x"/> and <paramref name="y"/> are equal; otherwise,
		/// <see langword="false"/>.
		/// </returns>
		public new bool Equals(object? x, object? y)
		{
			return LanguagePrimitives.Equals(x, y, this.IgnoreCase);
		}

		/// <summary>
		/// Returns a hash code for the specified object that is consistent with <see cref="Equals(object, object)"/>.
		/// </summary>
		/// <remarks>
		/// A string hashes with <see cref="StringComparer.InvariantCultureIgnoreCase"/>, or with
		/// <see cref="StringComparer.InvariantCulture"/> when <see cref="IgnoreCase"/> is <see langword="false"/>.
		/// Any other object that PowerShell can convert to a string hashes as that string, so values such as
		/// <c>1</c> and <c>"1"</c> produce the same hash code. An object that can't be converted uses its own
		/// <see cref="object.GetHashCode"/>.
		/// </remarks>
		/// <param name="obj">The object to hash. This value must not be <see langword="null"/>.</param>
		/// <returns>The hash code for <paramref name="obj"/>.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is null.</exception>
		public int GetHashCode(object? obj)
		{
			Guard.NotNull(obj, nameof(obj));

			if (obj is string s)
			{
				return this.IgnoreCase
					? StringComparer.InvariantCultureIgnoreCase.GetHashCode(s)
					: StringComparer.InvariantCulture.GetHashCode(s);
			}
			else if (LanguagePrimitives.TryConvertTo(obj, out string? resStr) && resStr is not null)
			{
				return this.IgnoreCase
					? StringComparer.InvariantCultureIgnoreCase.GetHashCode(resStr)
					: StringComparer.InvariantCulture.GetHashCode(resStr);
			}
			else
			{
				return obj.GetHashCode();
			}
		}
	}
}

