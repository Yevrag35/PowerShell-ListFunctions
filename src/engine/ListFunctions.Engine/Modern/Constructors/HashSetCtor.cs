namespace ListFunctions.Modern.Constructors;

public sealed class HashSetCtor : EqualityCollectionCtor<HashSet<object>>
{
	public static readonly Type HashSetTypeDefinition = typeof(HashSet<>);
	readonly Type _equalityType;

	public HashSetCtor(Type? genericType, IEqualityComparer? equalityComparer)
		: base(HashSetTypeDefinition, equalityComparer, ToArrayOrEmpty(genericType), null)
	{
		_equalityType = genericType ?? typeof(object);
	}
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
	protected sealed override Type GetTypeForEquality()
	{
		return _equalityType;
	}
	protected override bool ShouldConstructDefault(IEqualityComparer? comparer, Type[] genericTypes)
	{
		return (typeof(object).Equals(_equalityType) && (comparer is null || EqualityComparer<object>.Default.Equals(comparer)))
			   ||
			   base.ShouldConstructDefault(comparer, genericTypes);
	}

	private sealed class ObjectEqualityComparer : IEqualityComparer<object?>, IEqualityComparer
	{
		internal bool IgnoreCase { get; set; }

		public new bool Equals(object? x, object? y)
		{
			return LanguagePrimitives.Equals(x, y, this.IgnoreCase);
		}

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

