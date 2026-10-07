namespace ListFunctions.Internal;

/// <summary>
/// Provides a non-generic front end over a strongly typed <see cref="List{T}"/> whose element type is known only at runtime.
/// </summary>
/// <remarks>
/// <para>
/// Each added item is converted to the element type with PowerShell's conversion rules, so the list stores what
/// <c>$list.Add($item)</c> stores in PowerShell. Items that fail to convert are skipped and reported through
/// <see cref="ConversionFailed"/> instead of throwing.
/// </para>
/// <para>
/// Create instances with <see cref="CreateTyped(Type, uint, bool)"/>.
/// </para>
/// <para>
/// This type is not thread-safe. Synchronize access externally when multiple threads use the same instance.
/// </para>
/// </remarks>
internal abstract class ListWrapper
{
	/// <summary>
	/// Gets the number of elements in the underlying list.
	/// </summary>
	/// <value>
	/// The number of items that were converted and added. Skipped items are not counted.
	/// </value>
	public abstract int Count { get; }

	/// <summary>
	/// Gets or sets a value indicating whether <see langword="null"/> items and <see langword="null"/> conversion results are added to the list.
	/// </summary>
	/// <remarks>
	/// When this property is <see langword="true"/>, a <see langword="null"/> item is still converted to the element type first, so
	/// the stored value follows PowerShell's rules: for example, <c>0</c> for <see cref="int"/>, an empty string for
	/// <see cref="string"/>, and <see langword="null"/> for <see cref="Nullable{T}"/> and most reference types. Changing the value
	/// affects only items added afterward.
	/// </remarks>
	/// <value>
	/// <see langword="true"/> to add <see langword="null"/> items and <see langword="null"/> conversion results; <see langword="false"/>
	/// to skip them. The default is <see langword="false"/>.
	/// </value>
	public bool IncludeNulls { get; set; }

	/// <summary>
	/// Gets or sets the callback that is invoked when an item cannot be converted to the element type.
	/// </summary>
	/// <remarks>
	/// The callback receives the original item and the <see cref="PSInvalidCastException"/> that the conversion threw. The item
	/// is not added to the list, and adding continues with the next item. When this property is <see langword="null"/>, items that
	/// fail to convert are skipped silently.
	/// </remarks>
	/// <value>
	/// The callback to invoke for each item that fails to convert, or <see langword="null"/> to skip such items without notification.
	/// </value>
	public Action<object?, PSInvalidCastException>? ConversionFailed { get; set; }

	/// <summary>
	/// Converts and adds each item in the specified span to the underlying list.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Items are added in span order. Each item is subject to the <see cref="IncludeNulls"/> setting, and an item that fails
	/// to convert is reported through <see cref="ConversionFailed"/> and skipped.
	/// </para>
	/// <para>
	/// The span is read only for the duration of the call, and no reference to it is kept.
	/// </para>
	/// </remarks>
	/// <param name="items">The items to convert and add. The span may be empty and may contain <see langword="null"/> elements.</param>
	public void AddRange(ReadOnlySpan<object?> items)
	{
		foreach (object? item in items)
		{
			this.AddItem(item);
		}
	}

	/// <summary>
	/// Returns the underlying list as a non-generic <see cref="IList"/>.
	/// </summary>
	/// <remarks>
	/// The returned list is the wrapper's own storage, not a copy. Items added through the wrapper afterward appear in it, and
	/// changes made through it are visible to the wrapper.
	/// </remarks>
	/// <returns>The underlying list.</returns>
	public abstract IList AsList();

	/// <summary>
	/// Converts the specified item to the element type and adds it to the underlying list.
	/// </summary>
	/// <remarks>
	/// Implementations skip the item when it, or its converted value, is <see langword="null"/> and <see cref="IncludeNulls"/> is
	/// <see langword="false"/>. When the conversion fails, they invoke <see cref="ConversionFailed"/> and skip the item instead of
	/// throwing.
	/// </remarks>
	/// <param name="item">The item to convert and add. This value can be <see langword="null"/>.</param>
	protected abstract void AddItem(object? item);

	/// <summary>
	/// Sets the initial capacity of the underlying list.
	/// </summary>
	/// <param name="capacity">The number of elements the list can hold before it resizes. A value of <c>0</c> leaves the capacity unchanged.</param>
	protected abstract void SetCapacity(uint capacity);

	/// <summary>
	/// Creates a wrapper over a new <see cref="List{T}"/> whose element type is <paramref name="elementType"/>.
	/// </summary>
	/// <remarks>
	/// The returned instance is a closed <see cref="ListWrapper{T}"/> that is constructed through reflection.
	/// </remarks>
	/// <param name="elementType">The element type of the list. This value must not be <see langword="null"/>.</param>
	/// <param name="capacity">
	/// The initial capacity of the list. A value of <c>0</c> leaves the list empty with no preallocated storage. Values greater
	/// than <see cref="int.MaxValue"/> are clamped to <see cref="int.MaxValue"/>.
	/// </param>
	/// <param name="includeNulls">The initial value of <see cref="IncludeNulls"/>.</param>
	/// <returns>A new, empty wrapper whose underlying list stores elements of type <paramref name="elementType"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="elementType"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="elementType"/> cannot be used as a generic type argument, such as a pointer, by-ref, or
	/// <see cref="Void"/> type.
	/// </exception>
	/// <exception cref="OutOfMemoryException">Thrown when <paramref name="capacity"/> exceeds the maximum array length.</exception>
	public static ListWrapper CreateTyped(Type elementType, uint capacity, bool includeNulls = false)
	{
		ArgumentNullException.ThrowIfNull(elementType);
		Type listType = typeof(ListWrapper<>).MakeGenericType(elementType);
		ListWrapper wrapper = (ListWrapper)Activator.CreateInstance(listType)!;
		wrapper.SetCapacity(capacity);
		wrapper.IncludeNulls = includeNulls;
		return wrapper;
	}
}

/// <summary>
/// Wraps a <see cref="List{T}"/> and converts each added item to <typeparamref name="T"/> with PowerShell's conversion rules.
/// </summary>
/// <remarks>
/// <para>
/// The list starts with a capacity of <c>0</c> and allocates no element storage until an item is added or a capacity is set.
/// </para>
/// <para>
/// This type is not thread-safe. Synchronize access externally when multiple threads use the same instance.
/// </para>
/// </remarks>
/// <typeparam name="T">The element type of the underlying list.</typeparam>
internal sealed class ListWrapper<T> : ListWrapper
{
	[SuppressMessage("Style", "IDE0028", Justification = "Keeps an empty array on creation.")]
	private readonly List<T> _list = new(0);

	/// <inheritdoc/>
	public override int Count => _list.Count;

	/// <summary>
	/// Initializes a new <see cref="ListWrapper{T}"/> instance with an empty list that has a capacity of <c>0</c>.
	/// </summary>
	/// <remarks>
	/// <see cref="ListWrapper.CreateTyped(Type, uint, bool)"/> calls this constructor through reflection when the element type is
	/// known only at runtime.
	/// </remarks>
	public ListWrapper()
	{
	}

	/// <summary>
	/// Returns the underlying <see cref="List{T}"/> as a non-generic <see cref="IList"/>.
	/// </summary>
	/// <remarks>
	/// The returned list is the wrapper's own storage, not a copy. Callers can cast it back to a <see cref="List{T}"/>
	/// whose type argument is <typeparamref name="T"/>.
	/// </remarks>
	/// <returns>The underlying list.</returns>
	public override IList AsList()
	{
		return _list;
	}

	/// <summary>
	/// Sets the capacity of the underlying list, clamped to <see cref="int.MaxValue"/>.
	/// </summary>
	/// <param name="capacity">The number of elements the list can hold before it resizes. A value of <c>0</c> leaves the capacity unchanged.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is less than <see cref="Count"/>.</exception>
	/// <exception cref="OutOfMemoryException">Thrown when <paramref name="capacity"/> exceeds the maximum array length.</exception>
	protected override void SetCapacity(uint capacity)
	{
		if (capacity == 0) return;

		_list.Capacity = (int)Math.Min(capacity, int.MaxValue);
	}

	/// <summary>
	/// Converts the specified item to <typeparamref name="T"/> and adds it to the underlying list.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A <see langword="null"/> item is skipped before conversion when <see cref="ListWrapper.IncludeNulls"/> is
	/// <see langword="false"/>. Otherwise the item is converted, and a <see langword="null"/> result is added only when
	/// <see cref="ListWrapper.IncludeNulls"/> is <see langword="true"/>.
	/// </para>
	/// <para>
	/// When the conversion throws a <see cref="PSInvalidCastException"/>, the item is passed to
	/// <see cref="ListWrapper.ConversionFailed"/>, if it is set, and is not added.
	/// </para>
	/// </remarks>
	/// <param name="item">The item to convert and add. This value can be <see langword="null"/>.</param>
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

		if (value is not null || this.IncludeNulls)
		{
			_list.Add(value);
		}
	}
}
