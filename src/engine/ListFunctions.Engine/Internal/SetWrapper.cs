using ListFunctions.Extensions;
using ListFunctions.Modern;

namespace ListFunctions.Internal;

/// <summary>
/// Provides a non-generic front end over a strongly typed <see cref="HashSet{T}"/> or <see cref="SortedSet{T}"/> whose
/// element type is known only at run time.
/// </summary>
/// <remarks>
/// <para>
/// Each added item is converted to the element type with PowerShell's conversion rules, so the set stores what
/// <c>$set.Add($item)</c> stores in PowerShell. An item that fails to convert is skipped and reported through
/// <see cref="ConversionFailed"/> instead of throwing.
/// </para>
/// <para>
/// The conversions and the calls to the set run inside the closed <see cref="SetWrapper{T}"/>, so adding an item doesn't
/// go through reflection. An exception from the set's comparer, such as an error from the script blocks of an
/// <see cref="EqualityBlock"/> or a <see cref="ComparingBlock{T}"/>, reaches the caller as it is, not inside a
/// <see cref="TargetInvocationException"/>.
/// </para>
/// <para>
/// Create instances with <see cref="CreateHashSet(Type, uint, IEqualityComparer, bool)"/> or
/// <see cref="CreateSortedSet(Type, IComparer, bool)"/>.
/// </para>
/// <para>
/// This type is not thread-safe. Synchronize access externally when multiple threads use the same instance.
/// </para>
/// </remarks>
internal abstract class SetWrapper
{
	/// <summary>
	/// The generic method definition of <see cref="CreateHashSet{T}(int, IEqualityComparer)"/>.
	/// </summary>
	/// <remarks>
	/// The definition comes from a delegate to one closed form of the method, so the compiler checks the method's name and
	/// signature, and no lookup by name can miss it.
	/// </remarks>
	private static readonly MethodInfo s_createHashSetDefinition = new HashSetFactory(CreateHashSet<object>).Method.GetGenericMethodDefinition();

	/// <summary>
	/// The generic method definition of <see cref="CreateSortedSet{T}(IComparer)"/>.
	/// </summary>
	/// <remarks>
	/// The definition comes from a delegate to one closed form of the method, the same way as
	/// <see cref="s_createHashSetDefinition"/>.
	/// </remarks>
	private static readonly MethodInfo s_createSortedSetDefinition = new SortedSetFactory(CreateSortedSet<object>).Method.GetGenericMethodDefinition();

	/// <summary>
	/// Gets or sets a value indicating whether <see langword="null"/> items and <see langword="null"/> conversion results
	/// are added to the set.
	/// </summary>
	/// <remarks>
	/// When this property is <see langword="true"/>, a <see langword="null"/> item is still converted to the element type
	/// first, so the stored value follows PowerShell's rules: for example, <c>0</c> for <see cref="int"/>, an empty string
	/// for <see cref="string"/>, and <see langword="null"/> for <see cref="object"/>, <see cref="Nullable{T}"/>, and most
	/// other reference types. Changing the value affects only items added afterward.
	/// </remarks>
	/// <value>
	/// <see langword="true"/> to add <see langword="null"/> items and <see langword="null"/> conversion results;
	/// <see langword="false"/> to skip them. The default is <see langword="false"/>.
	/// </value>
	public bool IncludeNulls { get; set; }

	/// <summary>
	/// Gets or sets the callback that is invoked when an item cannot be converted to the element type.
	/// </summary>
	/// <remarks>
	/// The callback receives the original item and the <see cref="PSInvalidCastException"/> that the conversion threw. The
	/// item is not added to the set, and adding continues with the next item. When this property is
	/// <see langword="null"/>, items that fail to convert are skipped silently.
	/// </remarks>
	/// <value>
	/// The callback to invoke for each item that fails to convert, or <see langword="null"/> to skip such items without
	/// notification.
	/// </value>
	public Action<object?, PSInvalidCastException>? ConversionFailed { get; set; }

	/// <summary>
	/// Gets or sets the callback that is invoked when the set's <c>Add</c> method throws for an item.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The callback receives the item as it was before conversion and the exception that the set threw, such as one from
	/// the element type's own <see cref="object.GetHashCode"/> or comparison method. The item is not added, and adding
	/// continues with the next item.
	/// </para>
	/// <para>
	/// A <see cref="RuntimeException"/> or a <see cref="FlowControlException"/>, such as an error or a <c>break</c> from
	/// the script blocks of an <see cref="EqualityBlock"/> or a <see cref="ComparingBlock{T}"/>, never reaches the
	/// callback. It reaches the caller of <see cref="AddRange(ReadOnlySpan{object})"/> unchanged, so PowerShell handles it
	/// the way it handles one from any other script block. When this property is <see langword="null"/>, every exception
	/// reaches the caller.
	/// </para>
	/// </remarks>
	/// <value>
	/// The callback to invoke for each item that the set fails to add, or <see langword="null"/> to let the exception reach
	/// the caller.
	/// </value>
	public Action<object?, Exception>? AddFailed { get; set; }

	/// <summary>
	/// Converts and adds each item in the specified span to the underlying set.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Items are added in span order. Each item is subject to the <see cref="IncludeNulls"/> setting. An item that fails
	/// to convert is reported through <see cref="ConversionFailed"/> and skipped, and an item that the set fails to add is
	/// reported through <see cref="AddFailed"/>, if it is set, and skipped. An item that equals one already in the set is
	/// skipped without a report.
	/// </para>
	/// <para>
	/// The span is read only for the duration of the call, and no reference to it is kept.
	/// </para>
	/// </remarks>
	/// <param name="items">The items to convert and add. The span may be empty and may contain <see langword="null"/> elements.</param>
	/// <exception cref="RuntimeException">Thrown when the set's comparer throws one, for example because a script block of an <see cref="EqualityBlock"/> or a <see cref="ComparingBlock{T}"/> fails. The items before it stay in the set.</exception>
	/// <exception cref="FlowControlException">Thrown when the set's comparer throws one, for example because such a script block runs <c>break</c>.</exception>
	public void AddRange(ReadOnlySpan<object?> items)
	{
		foreach (object? item in items)
		{
			this.AddItem(item);
		}
	}

	/// <summary>
	/// Returns the underlying set as a non-generic <see cref="IEnumerable"/>.
	/// </summary>
	/// <remarks>
	/// The returned set is the wrapper's own storage, not a copy. Items added through the wrapper afterward appear in it,
	/// and changes made through it are visible to the wrapper. <see cref="HashSet{T}"/> implements no non-generic
	/// collection interface, so <see cref="IEnumerable"/> is the most specific type that both kinds of set share.
	/// </remarks>
	/// <returns>The underlying set.</returns>
	public abstract IEnumerable AsSet();

	/// <summary>
	/// Converts the specified item to the element type and adds it to the underlying set.
	/// </summary>
	/// <remarks>
	/// Implementations skip the item when it, or its converted value, is <see langword="null"/> and
	/// <see cref="IncludeNulls"/> is <see langword="false"/>. When the conversion fails, they invoke
	/// <see cref="ConversionFailed"/> and skip the item instead of throwing. When the set's <c>Add</c> method throws an
	/// exception that isn't PowerShell's own, they pass it to <see cref="AddFailed"/>, if it is set, instead of throwing it.
	/// </remarks>
	/// <param name="item">The item to convert and add. This value can be <see langword="null"/>.</param>
	protected abstract void AddItem(object? item);

	/// <summary>
	/// Creates a wrapper over a new <see cref="HashSet{T}"/> whose element type is <paramref name="elementType"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When <paramref name="comparer"/> is <see langword="null"/>, <see cref="string"/> and <see cref="object"/> elements
	/// compare with <see cref="StringComparer.OrdinalIgnoreCase"/>, or with <see cref="StringComparer.Ordinal"/> when
	/// <paramref name="caseSensitive"/> is <see langword="true"/>. A <see cref="StringComparer"/> compares two strings as
	/// strings, and any other two elements with their own <see cref="object.Equals(object)"/> and
	/// <see cref="object.GetHashCode"/> methods, so <c>1</c> and <c>"1"</c> are different elements. Elements of any other
	/// type compare with <see cref="EqualityComparer{T}.Default"/>.
	/// </para>
	/// <para>
	/// A comparer that isn't an <see cref="IEqualityComparer{T}"/> of the element type, such as a
	/// <see cref="StringComparer"/> for <see cref="object"/> elements or an <see cref="EqualityBlock"/> for
	/// <see cref="int"/> elements, is wrapped in an <see cref="EqualityComparerAdapter{T}"/>.
	/// </para>
	/// <para>
	/// The method closes a generic factory method over the element type and calls it through a delegate, not through
	/// <see cref="Activator.CreateInstance(Type, object[])"/>. So an exception from the set's constructor, such as the
	/// <see cref="OutOfMemoryException"/> for a capacity that no array can hold, reaches the caller as it is. On .NET 10,
	/// <see cref="Activator.CreateInstance(Type, object[])"/> would wrap it in a <see cref="TargetInvocationException"/>.
	/// </para>
	/// <para>
	/// .NET Standard 2.0 has no <see cref="HashSet{T}"/> constructor that takes a capacity, so on that target the factory
	/// method calls the one that .NET Framework 4.7.2 and later have through
	/// <see cref="Activator.CreateInstance(Type, object[])"/> when <paramref name="capacity"/> isn't <c>0</c>. .NET
	/// Framework doesn't wrap the <see cref="OutOfMemoryException"/>, so it still reaches the caller as it is.
	/// </para>
	/// </remarks>
	/// <param name="elementType">The element type of the set. This value must not be <see langword="null"/>.</param>
	/// <param name="capacity">
	/// The initial capacity of the set. A value of <c>0</c> leaves the set empty with no preallocated storage. Values
	/// greater than <see cref="int.MaxValue"/> are clamped to <see cref="int.MaxValue"/>.
	/// </param>
	/// <param name="comparer">The equality comparer for the elements, or <see langword="null"/> to use the default for the element type.</param>
	/// <param name="caseSensitive">
	/// <see langword="true"/> to have the default comparer of <see cref="string"/> and <see cref="object"/> elements
	/// consider case; otherwise, <see langword="false"/>. The value has no effect when <paramref name="comparer"/> isn't
	/// <see langword="null"/>.
	/// </param>
	/// <returns>A new, empty wrapper whose underlying set stores elements of type <paramref name="elementType"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="elementType"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="elementType"/> cannot be used as a generic type argument, such as a pointer, by-ref, or
	/// <see cref="Void"/> type.
	/// </exception>
	/// <exception cref="OutOfMemoryException">Thrown when <paramref name="capacity"/> exceeds the maximum array length.</exception>
	public static SetWrapper CreateHashSet(Type elementType, uint capacity, IEqualityComparer? comparer, bool caseSensitive = false)
	{
		ArgumentNullException.ThrowIfNull(elementType);

		comparer ??= GetDefaultEqualityComparer(elementType, caseSensitive);
		var create = (HashSetFactory)s_createHashSetDefinition
			.MakeGenericMethod(elementType)
#if NETCOREAPP
			.CreateDelegate<HashSetFactory>();
#else
			.CreateDelegate(typeof(HashSetFactory));
#endif

		return create((int)Math.Min(capacity, int.MaxValue), comparer);
	}

	/// <summary>
	/// Creates a wrapper over a new <see cref="SortedSet{T}"/> whose element type is <paramref name="elementType"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When <paramref name="comparer"/> is <see langword="null"/>, <see cref="string"/> elements compare with
	/// <see cref="StringComparer.OrdinalIgnoreCase"/>, or with <see cref="StringComparer.Ordinal"/> when
	/// <paramref name="caseSensitive"/> is <see langword="true"/>. <see cref="StringComparer.OrdinalIgnoreCase"/> orders
	/// strings as if they were uppercase, by the numeric values of their characters, so the order is the same in every
	/// culture. Elements of any other type compare with <see cref="Comparer{T}.Default"/>.
	/// </para>
	/// <para>
	/// Unlike an equality comparer, a comparer isn't wrapped: it must be an <see cref="IComparer{T}"/> of the element type,
	/// as a <see cref="ComparingBlock{T}"/> closed over that type is. The type check honors variance, so an
	/// <see cref="IComparer{T}"/> of a base type of a reference type qualifies too.
	/// </para>
	/// <para>
	/// The method closes a generic factory method over the element type and calls it through a delegate, the same way as
	/// <see cref="CreateHashSet(Type, uint, IEqualityComparer, bool)"/>.
	/// </para>
	/// </remarks>
	/// <param name="elementType">The element type of the set. This value must not be <see langword="null"/>.</param>
	/// <param name="comparer">
	/// The comparer that orders the elements, which must be an <see cref="IComparer{T}"/> of
	/// <paramref name="elementType"/>, or <see langword="null"/> to use the default for the element type.
	/// </param>
	/// <param name="caseSensitive">
	/// <see langword="true"/> to have the default comparer of <see cref="string"/> elements consider case; otherwise,
	/// <see langword="false"/>. The value has no effect when <paramref name="comparer"/> isn't <see langword="null"/>.
	/// </param>
	/// <returns>A new, empty wrapper whose underlying set stores elements of type <paramref name="elementType"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="elementType"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="elementType"/> cannot be used as a generic type argument, such as a pointer, by-ref, or
	/// <see cref="Void"/> type, or when <paramref name="comparer"/> isn't an <see cref="IComparer{T}"/> of it.
	/// </exception>
	public static SetWrapper CreateSortedSet(Type elementType, IComparer? comparer, bool caseSensitive = false)
	{
		ArgumentNullException.ThrowIfNull(elementType);

		comparer ??= GetDefaultSortComparer(elementType, caseSensitive);
		var create = (SortedSetFactory)s_createSortedSetDefinition
			.MakeGenericMethod(elementType)
#if NETCOREAPP
			.CreateDelegate<SortedSetFactory>();
#else
			.CreateDelegate(typeof(SortedSetFactory));
#endif

		return create(comparer);
	}

	/// <summary>
	/// Creates a <see cref="SetWrapper{T}"/> over a new <see cref="HashSet{T}"/> with the specified capacity and equality
	/// comparer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A comparer that isn't an <see cref="IEqualityComparer{T}"/> of <typeparamref name="T"/> is wrapped in an
	/// <see cref="EqualityComparerAdapter{T}"/>. The type check honors variance, so an <see cref="EqualityBlock"/>, which is
	/// an <see cref="IEqualityComparer{T}"/> of <see cref="object"/>, is used as it is for <see cref="string"/> elements and
	/// other reference types, and is wrapped for value types, such as <see cref="int"/> and <see cref="Nullable{T}"/>.
	/// </para>
	/// <para>
	/// <see cref="CreateHashSet(Type, uint, IEqualityComparer, bool)"/> closes this method over the element type and
	/// calls it through a <see cref="HashSetFactory"/> delegate. It chooses the default comparer for <see cref="string"/>
	/// and <see cref="object"/> elements before it does.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">The element type of the set.</typeparam>
	/// <param name="capacity">The initial capacity of the set, from 0 through <see cref="int.MaxValue"/>.</param>
	/// <param name="comparer">The equality comparer for the elements, or <see langword="null"/> to use <see cref="EqualityComparer{T}.Default"/>.</param>
	/// <returns>The new wrapper.</returns>
	/// <exception cref="OutOfMemoryException">Thrown when <paramref name="capacity"/> exceeds the maximum array length.</exception>
	private static SetWrapper CreateHashSet<T>(int capacity, IEqualityComparer? comparer)
	{
		IEqualityComparer<T>? typedComparer = comparer switch
		{
			null => null,
			IEqualityComparer<T> typed => typed,
			_ => new EqualityComparerAdapter<T>(comparer),
		};

#if NETCOREAPP
		HashSet<T> set = new(capacity, typedComparer);
#else
		// .NET Standard 2.0 has no HashSet<T>(int, IEqualityComparer<T>) constructor, but .NET Framework 4.7.2 and later
		// have it.
		HashSet<T> set = capacity == 0
			? new HashSet<T>(typedComparer)
			: (HashSet<T>)Activator.CreateInstance(typeof(HashSet<T>), capacity, typedComparer)!;
#endif

		return new SetWrapper<T>(set);
	}

	/// <summary>
	/// Creates a <see cref="SetWrapper{T}"/> over a new <see cref="SortedSet{T}"/> with the specified comparer.
	/// </summary>
	/// <remarks>
	/// <see cref="CreateSortedSet(Type, IComparer, bool)"/> closes this method over the element type and calls it through
	/// a <see cref="SortedSetFactory"/> delegate. It chooses the default comparer for <see cref="string"/> elements before
	/// it does.
	/// </remarks>
	/// <typeparam name="T">The element type of the set.</typeparam>
	/// <param name="comparer">
	/// The comparer that orders the elements, which must be an <see cref="IComparer{T}"/> of <typeparamref name="T"/>, or
	/// <see langword="null"/> to use <see cref="Comparer{T}.Default"/>.
	/// </param>
	/// <returns>The new wrapper.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="comparer"/> isn't an <see cref="IComparer{T}"/> of <typeparamref name="T"/>.</exception>
	private static SetWrapper CreateSortedSet<T>(IComparer? comparer)
	{
		IComparer<T>? typedComparer = comparer switch
		{
			null => null,
			IComparer<T> typed => typed,
			_ => throw new ArgumentException(
				$"Cannot sort elements of type [{typeof(T).GetTypeName()}] with a comparer of type "
				+ $"[{comparer.GetType().GetTypeName()}], because it doesn't compare values of that type.",
				nameof(comparer)),
		};

		return new SetWrapper<T>(new SortedSet<T>(typedComparer));
	}

	/// <summary>
	/// Returns the equality comparer that a hash set with the specified element type uses when no comparer is given.
	/// </summary>
	/// <param name="elementType">The element type of the set.</param>
	/// <param name="caseSensitive"><see langword="true"/> to compare strings with regard to case; otherwise, <see langword="false"/>.</param>
	/// <returns>
	/// <see cref="StringComparer.Ordinal"/> or <see cref="StringComparer.OrdinalIgnoreCase"/> for <see cref="string"/> and
	/// <see cref="object"/> elements; otherwise, <see langword="null"/>, which gives the set
	/// <see cref="EqualityComparer{T}.Default"/>.
	/// </returns>
	private static StringComparer? GetDefaultEqualityComparer(Type elementType, bool caseSensitive)
	{
		if (!typeof(string).Equals(elementType) && !typeof(object).Equals(elementType))
		{
			return null;
		}

		return caseSensitive
			? StringComparer.Ordinal
			: StringComparer.OrdinalIgnoreCase;
	}

	/// <summary>
	/// Returns the comparer that a sorted set with the specified element type uses when no comparer is given.
	/// </summary>
	/// <param name="elementType">The element type of the set.</param>
	/// <param name="caseSensitive"><see langword="true"/> to compare strings with regard to case; otherwise, <see langword="false"/>.</param>
	/// <returns>
	/// <see cref="StringComparer.Ordinal"/> or <see cref="StringComparer.OrdinalIgnoreCase"/> for <see cref="string"/>
	/// elements; otherwise, <see langword="null"/>, which gives the set <see cref="Comparer{T}.Default"/>.
	/// </returns>
	private static StringComparer? GetDefaultSortComparer(Type elementType, bool caseSensitive)
	{
		if (!typeof(string).Equals(elementType))
		{
			return null;
		}

		return caseSensitive
			? StringComparer.Ordinal
			: StringComparer.OrdinalIgnoreCase;
	}

	/// <summary>
	/// Represents <see cref="CreateHashSet{T}(int, IEqualityComparer)"/> after it's closed over an element type.
	/// </summary>
	/// <param name="capacity">The initial capacity of the set, from 0 through <see cref="int.MaxValue"/>.</param>
	/// <param name="comparer">The equality comparer for the elements, or <see langword="null"/> to use <see cref="EqualityComparer{T}.Default"/>.</param>
	/// <returns>The new wrapper.</returns>
	private delegate SetWrapper HashSetFactory(int capacity, IEqualityComparer? comparer);

	/// <summary>
	/// Represents <see cref="CreateSortedSet{T}(IComparer)"/> after it's closed over an element type.
	/// </summary>
	/// <param name="comparer">The comparer for the elements, or <see langword="null"/> to use <see cref="Comparer{T}.Default"/>.</param>
	/// <returns>The new wrapper.</returns>
	private delegate SetWrapper SortedSetFactory(IComparer? comparer);
}

/// <summary>
/// Wraps a <see cref="HashSet{T}"/> or a <see cref="SortedSet{T}"/> and converts each added item to
/// <typeparamref name="T"/> with PowerShell's conversion rules.
/// </summary>
/// <remarks>
/// <para>
/// The wrapper holds the set as an <see cref="ISet{T}"/>, so one class serves both kinds of set. The interface call
/// costs little next to PowerShell's conversion of each item.
/// </para>
/// <para>
/// This type is not thread-safe. Synchronize access externally when multiple threads use the same instance.
/// </para>
/// </remarks>
/// <typeparam name="T">The element type of the underlying set.</typeparam>
internal sealed class SetWrapper<T> : SetWrapper
{
	private readonly ISet<T> _set;

	/// <summary>
	/// Initializes a new <see cref="SetWrapper{T}"/> instance over the specified set.
	/// </summary>
	/// <remarks>
	/// <see cref="SetWrapper.CreateHashSet(Type, uint, IEqualityComparer, bool)"/> and
	/// <see cref="SetWrapper.CreateSortedSet(Type, IComparer, bool)"/> create the set and call this constructor when the
	/// element type is known only at run time.
	/// </remarks>
	/// <param name="set">The set to add items to. This value must not be <see langword="null"/>.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="set"/> is null.</exception>
	public SetWrapper(ISet<T> set)
	{
		ArgumentNullException.ThrowIfNull(set);
		_set = set;
	}

	/// <summary>
	/// Returns the underlying set as a non-generic <see cref="IEnumerable"/>.
	/// </summary>
	/// <remarks>
	/// The returned set is the wrapper's own storage, not a copy. Callers can cast it back to the
	/// <see cref="HashSet{T}"/> or <see cref="SortedSet{T}"/> whose type argument is <typeparamref name="T"/>.
	/// </remarks>
	/// <returns>The underlying set.</returns>
	public override IEnumerable AsSet()
	{
		return _set;
	}

	/// <summary>
	/// Converts the specified item to <typeparamref name="T"/> and adds it to the underlying set.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A <see langword="null"/> item is skipped before conversion when <see cref="SetWrapper.IncludeNulls"/> is
	/// <see langword="false"/>. Otherwise the item is converted, and a <see langword="null"/> result is added only when
	/// <see cref="SetWrapper.IncludeNulls"/> is <see langword="true"/>.
	/// </para>
	/// <para>
	/// When the conversion throws a <see cref="PSInvalidCastException"/>, the item is passed to
	/// <see cref="SetWrapper.ConversionFailed"/>, if it is set, and is not added. When the set's <c>Add</c> method throws
	/// an exception that isn't a <see cref="RuntimeException"/> or a <see cref="FlowControlException"/>, the item and the
	/// exception are passed to <see cref="SetWrapper.AddFailed"/>, if it is set, and the item is not added.
	/// </para>
	/// </remarks>
	/// <param name="item">The item to convert and add. This value can be <see langword="null"/>.</param>
	/// <exception cref="RuntimeException">Thrown when the set's comparer throws one, for example because a script block of an <see cref="EqualityBlock"/> or a <see cref="ComparingBlock{T}"/> fails.</exception>
	/// <exception cref="FlowControlException">Thrown when the set's comparer throws one, for example because such a script block runs <c>break</c>.</exception>
	protected override void AddItem(object? item)
	{
		if (item is null && !this.IncludeNulls)
		{
			return;
		}

		T value;
		try
		{
			value = LanguagePrimitives.ConvertTo<T>(item);
		}
		catch (PSInvalidCastException e)
		{
			this.ConversionFailed?.Invoke(item, e);
			return;
		}

		if (value is null && !this.IncludeNulls)
		{
			return;
		}

		try
		{
			_ = _set.Add(value);
		}
		catch (Exception e) when (this.AddFailed is { } addFailed && e is not (RuntimeException or FlowControlException))
		{
			addFailed(item, e);
		}
	}
}
